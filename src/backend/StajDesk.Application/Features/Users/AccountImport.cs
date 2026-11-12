using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Users;

/* =========================================================
   CSV ile toplu çalışan ekleme.
   Kapalı portal kuralı korunur: CSV HESAP AÇMAZ. Her geçerli satır için "yeni hesap talebi" oluşur;
   hesabı BT/Admin, Hesap talepleri ekranından onaylayarak açar (kullanıcı adı, rol, ilk şifre daveti).
   CSV'de olup sistemde olmayan departman/unvanlar yalnızca org.manage yetkisi olan kişi için oluşturulur.
   ========================================================= */

public sealed record AccountImportRowDto(int Line, string FullName, string Email, string? EmployeeNumber, string? Department, string? Position,
    EmploymentType EmploymentType, DateOnly? HireDate, string? Phone, bool Valid, IReadOnlyList<string> Errors, int? RequestId);

public sealed record AccountImportResultDto(bool DryRun, int Total, int ValidCount, int CreatedCount,
    IReadOnlyList<string> NewDepartments, IReadOnlyList<string> NewPositions, IReadOnlyList<AccountImportRowDto> Rows);

public interface IAccountImportService
{
    /// <summary>dryRun=true yalnızca doğrular (hiçbir şey kaydetmez); false ise geçerli satırlar için hesap talebi açar.</summary>
    Task<AccountImportResultDto> ImportAsync(Stream csv, bool dryRun, CancellationToken ct = default);
}

internal sealed class AccountImportService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit) : IAccountImportService
{
    public const int MaxRows = 5000;
    public const long MaxBytes = 5 * 1024 * 1024;
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Başlık eşleştirme: Türkçe karakter/boşluk/büyük-küçük harf farkı önemsizdir ("İşe Başlama" = "ise_baslama").</summary>
    private static readonly Dictionary<string, string> Columns = new()
    {
        ["adsoyad"] = "name", ["adisoyadi"] = "name", ["fullname"] = "name",
        ["eposta"] = "email", ["kurumsaleposta"] = "email", ["email"] = "email", ["mail"] = "email",
        ["personelkodu"] = "number", ["sicilno"] = "number", ["sicil"] = "number", ["employeenumber"] = "number",
        ["departman"] = "department", ["birim"] = "department", ["department"] = "department",
        ["unvan"] = "position", ["gorev"] = "position", ["title"] = "position", ["position"] = "position",
        ["personelturu"] = "type", ["calismaturu"] = "type", ["employmenttype"] = "type",
        ["isebaslama"] = "hire", ["isebaslamatarihi"] = "hire", ["hiredate"] = "hire",
        ["telefon"] = "phone", ["phone"] = "phone",
        ["not"] = "note", ["aciklama"] = "note", ["note"] = "note",
    };

    public async Task<AccountImportResultDto> ImportAsync(Stream csv, bool dryRun, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AccountsRequest, Permissions.UsersManage);
        var userId = currentUser.RequireUserId();
        var canCreateOrg = currentUser.Has(Permissions.OrgManage);

        var table = await ReadAsync(csv, ct);
        if (table.Count < 2) throw new DomainException("CSV dosyasında başlık satırı ve en az bir çalışan satırı olmalıdır.");
        if (table.Count - 1 > MaxRows) throw new DomainException($"Tek seferde en fazla {MaxRows} satır içe aktarılabilir.");

        var header = table[0].Select(h => Columns.GetValueOrDefault(Normalize(h))).ToList();
        foreach (var required in new[] { "name", "email", "number", "department", "position" })
            if (!header.Contains(required))
                throw new DomainException("CSV başlığında zorunlu sütunlar eksik. Gerekli: Ad Soyad; E-posta; Personel Kodu; Departman; Unvan. Örnek şablonu indirip kullanın.");

        var departments = await db.Departments.Where(d => d.IsActive).ToListAsync(ct);
        var positions = await db.Positions.ToListAsync(ct);
        var emails = table.Skip(1).Select(r => Cell(r, header, "email")?.ToLowerInvariant()).Where(e => e is not null).ToList();
        var existingEmails = (await db.Users.Where(u => emails.Contains(u.Email)).Select(u => u.Email).ToListAsync(ct))
            .Concat(await db.AccountRequests.Where(x => emails.Contains(x.Email) && x.Status == AccountRequestStatus.Pending && x.Type == AccountRequestType.NewAccount)
                .Select(x => x.Email).ToListAsync(ct))
            .ToHashSet();
        var numbers = table.Skip(1).Select(r => Cell(r, header, "number")).Where(n => n is not null).ToList();
        var existingNumbers = (await db.EmployeeProfiles.Where(p => p.EmployeeNumber != null && numbers.Contains(p.EmployeeNumber)).Select(p => p.EmployeeNumber!).ToListAsync(ct))
            .Concat(await db.AccountRequests.Where(x => x.EmployeeNumber != null && numbers.Contains(x.EmployeeNumber) && x.Status == AccountRequestStatus.Pending)
                .Select(x => x.EmployeeNumber!).ToListAsync(ct))
            .ToHashSet();

        var newDepartments = new Dictionary<string, Department>(StringComparer.OrdinalIgnoreCase);
        var newPositions = new Dictionary<string, Position>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>();
        var seenNumbers = new HashSet<string>();
        var rows = new List<(AccountImportRowDto Dto, AccountRequest? Entity)>();
        var validator = new CreateAccountRequestValidator();

        for (var i = 1; i < table.Count; i++)
        {
            var r = table[i];
            if (r.All(string.IsNullOrWhiteSpace)) continue;
            var errors = new List<string>();
            var name = Cell(r, header, "name") ?? "";
            var email = (Cell(r, header, "email") ?? "").ToLowerInvariant();
            var number = Cell(r, header, "number");
            var deptName = Cell(r, header, "department");
            var posName = Cell(r, header, "position");
            var phone = Cell(r, header, "phone");
            var note = Cell(r, header, "note");

            var type = ParseType(Cell(r, header, "type"), errors);
            var hireText = Cell(r, header, "hire");
            var hire = ParseDate(hireText, errors);

            // Departman: kod veya ad ile eşleşir; yoksa (yetki varsa) oluşturulacaklar listesine girer.
            Department? dept = null;
            if (deptName is not null)
            {
                dept = departments.FirstOrDefault(d => Same(d.Name, deptName) || Same(d.Code, deptName))
                       ?? newDepartments.GetValueOrDefault(deptName);
                if (dept is null && canCreateOrg)
                {
                    dept = new Department { Name = Text.Clean(deptName), Code = UniqueCode(deptName, departments, newDepartments.Values) };
                    newDepartments[deptName] = dept;
                }
                else if (dept is null) errors.Add($"\"{deptName}\" departmanı tanımlı değil (yeni departman açmak için Departman ve unvan yönetimi yetkisi gerekir).");
            }
            Position? pos = null;
            if (posName is not null)
            {
                pos = positions.FirstOrDefault(p => Same(p.Title, posName)) ?? newPositions.GetValueOrDefault(posName);
                if (pos is null && canCreateOrg)
                {
                    pos = new Position { Title = Text.Clean(posName), Level = 7 };
                    newPositions[posName] = pos;
                }
                else if (pos is null) errors.Add($"\"{posName}\" unvanı tanımlı değil (yeni unvan açmak için Departman ve unvan yönetimi yetkisi gerekir).");
            }

            // Alan kuralları tek formdaki ile aynı (departman/unvan kimliği burada henüz yok, sahte 0 verilir).
            var probe = new CreateAccountRequest(name, email, number, dept is null ? null : 0, pos is null ? null : 0, null, null, type, hire, phone, note);
            errors.AddRange(validator.Validate(probe).Errors
                // Ad yazılıp bulunamadıysa yukarıda daha açıklayıcı bir hata eklendi; genel "seçin" hatası tekrarlanmaz.
                .Where(e => !(e.PropertyName == nameof(CreateAccountRequest.DepartmentId) && deptName is not null)
                            && !(e.PropertyName == nameof(CreateAccountRequest.PositionId) && posName is not null)
                            && !(e.PropertyName == nameof(CreateAccountRequest.HireDate) && hireText is not null))
                .Select(e => e.ErrorMessage));

            if (email.Length > 0 && existingEmails.Contains(email)) errors.Add("Bu e-postayla bir hesap veya bekleyen talep zaten var.");
            else if (email.Length > 0 && !seenEmails.Add(email)) errors.Add("Bu e-posta dosyada birden fazla kez geçiyor.");
            if (number is not null && existingNumbers.Contains(number)) errors.Add($"{number} personel kodu başka bir çalışana veya bekleyen talebe ait.");
            else if (number is not null && !seenNumbers.Add(number)) errors.Add($"{number} personel kodu dosyada birden fazla kez geçiyor.");

            var distinct = errors.Distinct().ToList();
            AccountRequest? entity = null;
            if (distinct.Count == 0)
                entity = new AccountRequest
                {
                    Type = AccountRequestType.NewAccount, FullName = Text.Clean(name), Email = email, EmployeeNumber = Text.CleanOrNull(number),
                    Department = dept, Position = pos, EmploymentType = type, HireDate = hire,
                    Phone = Text.CleanOrNull(phone), Note = Text.CleanOrNull(note), RequestedById = userId,
                };
            rows.Add((new AccountImportRowDto(i + 1, name, email, number, deptName, posName, type, hire, phone, distinct.Count == 0, distinct, null), entity));
        }

        var valid = rows.Where(x => x.Entity is not null).ToList();
        // Yalnızca geçerli satırların ihtiyaç duyduğu departman/unvanlar oluşturulur.
        var usedDepartments = newDepartments.Where(d => valid.Any(v => v.Entity!.Department == d.Value)).Select(d => d.Value).ToList();
        var usedPositions = newPositions.Where(p => valid.Any(v => v.Entity!.Position == p.Value)).Select(p => p.Value).ToList();

        if (dryRun || valid.Count == 0)
            return new AccountImportResultDto(true, rows.Count, valid.Count, 0,
                newDepartments.Values.Select(d => d.Name).ToList(), newPositions.Values.Select(p => p.Title).ToList(), rows.Select(x => x.Dto).ToList());

        db.Departments.AddRange(usedDepartments);
        db.Positions.AddRange(usedPositions);
        db.AccountRequests.AddRange(valid.Select(v => v.Entity!));
        await db.SaveChangesAsync(ct);

        foreach (var d in usedDepartments) audit.Add("department.created", "department", d.Id, new { d.Code, source = "csv" });
        foreach (var p in usedPositions) audit.Add("position.created", "position", p.Id, new { p.Title, source = "csv" });
        foreach (var v in valid) audit.Add("account_request.created", "account_request", v.Entity!.Id, new { type = "NewAccount", source = "csv" });
        audit.Add("account_request.imported", "account_request", null, new { total = rows.Count, created = valid.Count });

        var reviewers = await db.Users.Where(u => u.IsActive && u.LockedAt == null && u.Id != userId &&
                u.Roles.Any(r => r.Role.Permissions.Any(p => p.Permission.Key == Permissions.UsersManage)))
            .Select(u => u.Id).ToListAsync(ct);
        foreach (var id in reviewers)
            db.Notifications.Add(new Notification
            {
                UserId = id, Title = $"CSV ile {valid.Count} yeni hesap talebi",
                Message = $"{currentUser.FullName} CSV dosyasıyla {valid.Count} çalışan için portal hesabı istedi. Hesap talepleri ekranından inceleyin.",
            });
        await db.SaveChangesAsync(ct);

        var created = rows.Select(x => x.Entity is null ? x.Dto : x.Dto with { RequestId = x.Entity.Id }).ToList();
        return new AccountImportResultDto(false, rows.Count, valid.Count, valid.Count,
            usedDepartments.Select(d => d.Name).ToList(), usedPositions.Select(p => p.Title).ToList(), created);
    }

    /* ---------------- Yardımcılar ---------------- */

    private static async Task<List<string[]>> ReadAsync(Stream csv, CancellationToken ct)
    {
        using var limited = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await csv.ReadAsync(buffer, ct)) > 0)
        {
            if (limited.Length + read > MaxBytes) throw new DomainException("CSV dosyası en fazla 5 MB olabilir.");
            limited.Write(buffer, 0, read);
        }
        var bytes = limited.ToArray();
        // Excel "CSV UTF-8" BOM'lu yazar; BOM'suz dosyalar Türkçe Windows kod sayfasıyla (1254) yazılmış olabilir.
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            text = Encoding.GetEncoding(1254).GetString(bytes);
        }
        text = text.TrimStart('﻿');
        var firstLine = text.Split('\n', 2)[0];
        // Türkçe Excel ayırıcı olarak ";" kullanır; virgül de desteklenir.
        var sep = firstLine.Count(c => c == ';') >= firstLine.Count(c => c == ',') ? ';' : ',';
        return Parse(text, sep);
    }

    /// <summary>RFC 4180 uyumlu basit ayrıştırıcı: tırnak içinde ayırıcı, satır sonu ve "" kaçışı desteklenir.</summary>
    internal static List<string[]> Parse(string text, char sep)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"' && cell.Length == 0) quoted = true;
            else if (c == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                rows.Add([.. row]); row.Clear();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add([.. row]); }
        // Excel dosya sonuna ";;;;" gibi tamamen boş satırlar ekleyebilir: bunlar satır sayısına girmez.
        return rows.Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
    }

    private static string? Cell(string[] row, List<string?> header, string column)
    {
        var index = header.IndexOf(column);
        if (index < 0 || index >= row.Length) return null;
        var v = row[index].Trim();
        return v.Length == 0 ? null : v;
    }

    internal static string Normalize(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Trim().ToLower(Tr))
            sb.Append(ch switch { 'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', _ => ch });
        return new string(sb.ToString().Where(char.IsAsciiLetterOrDigit).ToArray());
    }

    private static bool Same(string a, string b) => Normalize(a) == Normalize(b);

    private static EmploymentType ParseType(string? value, List<string> errors)
    {
        if (value is null) return EmploymentType.FullTime;
        var n = Normalize(value);
        if (n is "tamzamanli" or "fulltime" or "kadrolu" or "tam") return EmploymentType.FullTime;
        if (n is "yarizamanli" or "parttime" or "yari") return EmploymentType.PartTime;
        if (n is "sozlesmeli" or "contract" or "donemsel") return EmploymentType.Contract;
        if (n is "stajyer" or "intern" or "staj") return EmploymentType.Intern;
        errors.Add($"\"{value}\" personel türü tanınmadı (Tam zamanlı, Yarı zamanlı, Sözleşmeli, Stajyer).");
        return EmploymentType.FullTime;
    }

    private static DateOnly? ParseDate(string? value, List<string> errors)
    {
        if (value is null) return null;
        string[] formats = ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"];
        if (DateOnly.TryParseExact(value, formats, Tr, DateTimeStyles.None, out var d)) return d;
        errors.Add($"\"{value}\" tarihi okunamadı (ör. 01.12.2026).");
        return null;
    }

    /// <summary>Departman adından kısa, tekil kod: "Satış Operasyonları" → "SO", çakışırsa "SO2".</summary>
    private static string UniqueCode(string name, IEnumerable<Department> existing, IEnumerable<Department> pending)
    {
        var words = Normalize(name).Length == 0 ? ["DEP"] : name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Normalize).Where(w => w.Length > 0).ToArray();
        var baseCode = (words.Length == 1 ? words[0][..Math.Min(4, words[0].Length)] : string.Concat(words.Take(4).Select(w => w[0]))).ToUpperInvariant();
        if (baseCode.Length < 2) baseCode = (baseCode + "XX")[..2];
        var taken = existing.Select(d => d.Code).Concat(pending.Select(d => d.Code)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var code = baseCode;
        for (var i = 2; taken.Contains(code); i++) code = $"{baseCode}{i}";
        return code;
    }
}
