using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Account;
using StajDesk.Application.Features.Auth;
using StajDesk.Application.Features.Tickets;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Employees;

/* =========================================================
   Çalışanlar (personel). Üç farklı yetki katmanı:
   - Herkes (giriş yapmış): rehber + kamuya açık profil (ad, unvan, departman, iş telefonu, yönetici, işe başlama).
   - Kişinin kendisi + employees.read.private: kişisel bilgiler (doğum tarihi, adres, acil durum kişisi).
   - employees.manage (İK): personel (iş) bilgilerini düzenler.
   Hesap açma / kapatma / kilitleme ve sistem rolleri İK'nın değil BT'nin (users.manage) işidir:
   İK yeni çalışan veya ayrılan çalışan için "hesap talebi" açar (bkz. AccountRequests).
   ========================================================= */

public sealed record RefDto(int Id, string Name);

public sealed record EmployeeCardDto(int Id, string FullName, string Email, string? Phone, string? Title, RefDto? Department,
    StoreRefDto? Store, bool HasPhoto, string? PhotoVersion, bool IsActive);

public sealed record EmployeePrivateDto(DateOnly? BirthDate, string? PersonalEmail, string? Address,
    string? EmergencyContactName, string? EmergencyContactPhone, string? EmergencyContactRelation);

public sealed record EmployeeAccessDto(bool IsSelf, bool CanEditPersonal, bool CanEditWork, bool CanSeePrivate);

public sealed record EmployeeDetailDto(
    int Id, string FullName, string Email, string? Username, string? Phone, bool IsActive,
    RefDto? Position, int? PositionLevel, RefDto? Department, UserSummaryDto? Manager, StoreRefDto? Store,
    string? EmployeeNumber, DateOnly? HireDate, DateOnly? TerminationDate, EmploymentType EmploymentType,
    /// <summary>Doğum günü (ay-gün) herkese görünür; yıl yalnızca yetkiliye.</summary>
    string? Birthday,
    IReadOnlyList<EmployeeCardDto> DirectReports,
    bool HasPhoto, bool HasCover, string? PhotoVersion, string? CoverVersion,
    EmployeePrivateDto? Private,
    IReadOnlyList<string> Roles,
    bool EmailVerified, DateTime? LastLoginAt,
    EmployeeAccessDto Access);

public sealed record EmployeeQuery : PagedQuery
{
    public int? DepartmentId { get; init; }
    public int? PositionId { get; init; }
    public int? ManagerId { get; init; }
    /// <summary>Pasif (ayrılmış) personeli de getir — yalnızca İK.</summary>
    public bool? IncludeInactive { get; init; }
}

public sealed record UpdatePersonalRequest(string? Phone, string? PersonalEmail, string? Address, DateOnly? BirthDate,
    string? EmergencyContactName, string? EmergencyContactPhone, string? EmergencyContactRelation);

/// <summary>İK'nın düzenlediği personel bilgileri. Hesap durumu (aktif/pasif/kilit) ve kullanıcı adı burada YOK: onlar BT'nin işi.</summary>
public sealed record UpdateWorkRequest(string FullName, string? EmployeeNumber, int? DepartmentId, int? PositionId, int? ManagerId,
    int? StoreId, DateOnly? HireDate, DateOnly? TerminationDate, EmploymentType EmploymentType);

public sealed record PhotoFile(Stream Content, string ContentType);

public interface IEmployeeService
{
    Task<PagedResult<EmployeeCardDto>> ListAsync(EmployeeQuery query, CancellationToken ct = default);
    Task<EmployeeDetailDto> GetAsync(int id, CancellationToken ct = default);
    Task UpdatePersonalAsync(int id, UpdatePersonalRequest request, CancellationToken ct = default);
    Task UpdateWorkAsync(int id, UpdateWorkRequest request, CancellationToken ct = default);
    Task SetImageAsync(int id, string kind, string fileName, Stream content, CancellationToken ct = default);
    Task RemoveImageAsync(int id, string kind, CancellationToken ct = default);
    Task<PhotoFile?> GetImageAsync(int id, string kind, CancellationToken ct = default);
}

internal sealed class EmployeeService(
    IAppDbContext db, ICurrentUser currentUser, ICacheService cache, IAuditLogger audit, IFileStorage storage, IAccountRequestService accountRequests) : IEmployeeService
{
    public const long MaxImageBytes = 2 * 1024 * 1024;
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".webp"] = "image/webp",
    };

    private bool IsHr => currentUser.Has(Permissions.EmployeesManage);
    private bool SeesPrivate(int id) => id == currentUser.UserId || currentUser.HasAny(Permissions.EmployeesReadPrivate, Permissions.EmployeesManage);

    public async Task<PagedResult<EmployeeCardDto>> ListAsync(EmployeeQuery query, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var q = db.Users.AsNoTracking();
        if (!(query.IncludeInactive == true && IsHr)) q = q.Where(u => u.IsActive);
        // E-postasını doğrulamamış kendi kaydı rehberde görünmez (İK'nın davet ettiği, şifresini henüz belirlememiş çalışan görünür).
        if (!IsHr) q = q.Where(u => u.EmailVerifiedAt != null || u.PasswordHash == "");
        if (query.DepartmentId is not null) q = q.Where(u => u.DepartmentId == query.DepartmentId);
        if (query.PositionId is not null) q = q.Where(u => u.PositionId == query.PositionId);
        if (query.ManagerId is not null) q = q.Where(u => u.ManagerId == query.ManagerId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(u => u.FullName.ToLower().Contains(s) || u.Email.Contains(s)
                             || (u.Position != null && u.Position.Title.ToLower().Contains(s))
                             || (u.Department != null && u.Department.Name.ToLower().Contains(s))
                             || (u.Profile != null && u.Profile.EmployeeNumber == s));
        }
        return await q.OrderBy(u => u.FullName).Select(Card).ToPagedResultAsync(query, ct);
    }

    internal static readonly System.Linq.Expressions.Expression<Func<User, EmployeeCardDto>> Card = u => new EmployeeCardDto(
        u.Id, u.FullName, u.Email, u.Phone, u.Position == null ? null : u.Position.Title,
        u.Department == null ? null : new RefDto(u.Department.Id, u.Department.Name),
        u.Store == null ? null : new StoreRefDto(u.Store.Id, u.Store.Code, u.Store.Name),
        u.Profile != null && u.Profile.PhotoKey != null, u.Profile == null ? null : u.Profile.PhotoKey, u.IsActive);

    public async Task<EmployeeDetailDto> GetAsync(int id, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var u = await db.Users.AsNoTracking()
                    .Include(x => x.Position).Include(x => x.Department).Include(x => x.Manager).Include(x => x.Store)
                    .Include(x => x.Profile).Include(x => x.Roles).ThenInclude(r => r.Role)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Çalışan", id);
        if (!u.IsActive && !IsHr && id != me) throw NotFoundException.For("Çalışan", id);

        var reports = await db.Users.AsNoTracking().Where(x => x.ManagerId == id && x.IsActive).OrderBy(x => x.FullName).Select(Card).ToListAsync(ct);
        var seesPrivate = SeesPrivate(id);
        var p = u.Profile;
        var isSelf = id == me;
        return new EmployeeDetailDto(
            u.Id, u.FullName, u.Email, isSelf || IsHr || currentUser.Has(Permissions.UsersManage) ? u.Username : null, u.Phone, u.IsActive,
            u.Position is null ? null : new RefDto(u.Position.Id, u.Position.Title), u.Position?.Level,
            u.Department is null ? null : new RefDto(u.Department.Id, u.Department.Name),
            u.Manager is null ? null : new UserSummaryDto(u.Manager.Id, u.Manager.FullName, u.Manager.Email),
            u.Store is null ? null : new StoreRefDto(u.Store.Id, u.Store.Code, u.Store.Name),
            seesPrivate ? p?.EmployeeNumber : null, p?.HireDate, IsHr || isSelf ? p?.TerminationDate : null, p?.EmploymentType ?? EmploymentType.FullTime,
            p?.BirthDate?.ToString("MM-dd"),
            reports,
            p?.PhotoKey is not null, p?.CoverKey is not null, p?.PhotoKey, p?.CoverKey,
            seesPrivate && p is not null
                ? new EmployeePrivateDto(p.BirthDate, p.PersonalEmail, p.Address, p.EmergencyContactName, p.EmergencyContactPhone, p.EmergencyContactRelation)
                : seesPrivate ? new EmployeePrivateDto(null, null, null, null, null, null) : null,
            u.Roles.Select(r => r.Role.DisplayName).ToList(),
            u.EmailVerifiedAt is not null, isSelf || IsHr ? u.LastLoginAt : null,
            new EmployeeAccessDto(isSelf, isSelf || IsHr, IsHr, seesPrivate));
    }

    public async Task UpdatePersonalAsync(int id, UpdatePersonalRequest r, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        if (id != currentUser.UserId && !IsHr) throw new ForbiddenException("Kişisel bilgileri yalnızca kişinin kendisi veya İnsan Kaynakları değiştirebilir.");
        var u = await db.Users.Include(x => x.Profile).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Çalışan", id);
        u.Profile ??= new EmployeeProfile { UserId = u.Id };
        u.Phone = Text.CleanOrNull(r.Phone);
        u.Profile.PersonalEmail = Text.CleanOrNull(r.PersonalEmail)?.ToLowerInvariant();
        u.Profile.Address = Text.CleanOrNull(r.Address);
        u.Profile.BirthDate = r.BirthDate;
        u.Profile.EmergencyContactName = Text.CleanOrNull(r.EmergencyContactName);
        u.Profile.EmergencyContactPhone = Text.CleanOrNull(r.EmergencyContactPhone);
        u.Profile.EmergencyContactRelation = Text.CleanOrNull(r.EmergencyContactRelation);
        u.Profile.UpdatedAt = DateTime.UtcNow;
        // Değerler loglanmaz (kişisel veri); yalnızca kimin değiştirdiği kaydedilir.
        audit.Add("employee.personal_updated", "user", id);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateWorkAsync(int id, UpdateWorkRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.EmployeesManage);
        var u = await db.Users.Include(x => x.Profile).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Çalışan", id);
        await EnsureNumberFreeAsync(r.EmployeeNumber, id, ct);
        await EnsureRefsAsync(r.DepartmentId, r.PositionId, r.StoreId, ct);
        if (r.ManagerId is { } managerId) await EnsureNoCycleAsync(id, managerId, ct);

        var newTermination = r.TerminationDate is not null && u.Profile?.TerminationDate != r.TerminationDate;
        u.FullName = Text.Clean(r.FullName);
        u.DepartmentId = r.DepartmentId;
        u.PositionId = r.PositionId;
        u.ManagerId = r.ManagerId;
        u.StoreId = r.StoreId;
        u.UpdatedAt = DateTime.UtcNow;
        u.Profile ??= new EmployeeProfile { UserId = u.Id };
        u.Profile.EmployeeNumber = Text.CleanOrNull(r.EmployeeNumber);
        u.Profile.HireDate = r.HireDate;
        u.Profile.TerminationDate = r.TerminationDate;
        u.Profile.EmploymentType = r.EmploymentType;
        u.Profile.UpdatedAt = DateTime.UtcNow;
        audit.Add("employee.work_updated", "user", id, new { r.DepartmentId, r.PositionId, r.ManagerId, r.TerminationDate });
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Permissions(id), ct); // mağaza bir sonraki istekte geçerli olsun

        // Ayrılış tarihi girildiyse hesap İK tarafından KAPATILMAZ; BT'ye hesap kapatma talebi açılır.
        if (newTermination && u.IsActive && id != currentUser.UserId
            && !await db.AccountRequests.AnyAsync(x => x.TargetUserId == id && x.Status == AccountRequestStatus.Pending, ct))
            await accountRequests.CreateDeactivationAsync(new CreateDeactivationRequest(id, r.TerminationDate!.Value, "Personel bilgilerine ayrılış tarihi girildi."), ct);
    }

    // ---------- Profil ve kapak fotoğrafı ----------

    public async Task SetImageAsync(int id, string kind, string fileName, Stream content, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        if (id != currentUser.UserId && !IsHr) throw new ForbiddenException("Fotoğrafı yalnızca kişinin kendisi veya İnsan Kaynakları değiştirebilir.");
        var isCover = Kind(kind);
        var ext = Path.GetExtension(fileName);
        if (!ImageTypes.ContainsKey(ext)) throw new DomainException("Fotoğraf PNG, JPG veya WEBP olmalıdır.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0) throw new DomainException("Dosya boş görünüyor. Başka bir fotoğraf seçin.");
        if (buffer.Length > MaxImageBytes) throw new DomainException("Fotoğraf 2 MB'tan büyük olamaz. Küçültüp tekrar deneyin.");
        if (!AttachmentRules.SignatureMatches(ext, buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 64))))
            throw new DomainException("Dosyanın içeriği bir fotoğraf değil. PNG, JPG veya WEBP yükleyin.");

        var u = await db.Users.Include(x => x.Profile).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Çalışan", id);
        u.Profile ??= new EmployeeProfile { UserId = u.Id };
        var old = isCover ? u.Profile.CoverKey : u.Profile.PhotoKey;
        var key = Guid.NewGuid().ToString("N");
        buffer.Position = 0;
        await storage.SaveAsync(key, buffer, ct);
        if (isCover) u.Profile.CoverKey = key; else u.Profile.PhotoKey = key;
        u.Profile.UpdatedAt = DateTime.UtcNow;
        audit.Add(isCover ? "employee.cover_changed" : "employee.photo_changed", "user", id);
        await db.SaveChangesAsync(ct);
        if (old is not null) await storage.DeleteAsync(old, ct);
    }

    public async Task RemoveImageAsync(int id, string kind, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        if (id != currentUser.UserId && !IsHr) throw new ForbiddenException("Fotoğrafı yalnızca kişinin kendisi veya İnsan Kaynakları kaldırabilir.");
        var isCover = Kind(kind);
        var p = await db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == id, ct);
        var key = isCover ? p?.CoverKey : p?.PhotoKey;
        if (p is null || key is null) return;
        if (isCover) p.CoverKey = null; else p.PhotoKey = null;
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(key, ct);
    }

    public async Task<PhotoFile?> GetImageAsync(int id, string kind, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var isCover = Kind(kind);
        var key = await db.EmployeeProfiles.AsNoTracking().Where(x => x.UserId == id && x.User.IsActive)
            .Select(x => isCover ? x.CoverKey : x.PhotoKey).FirstOrDefaultAsync(ct);
        if (key is null) return null;
        var stream = await storage.OpenReadAsync(key, ct);
        if (stream is null) return null;
        // Tür dosyanın imzasından okunur (yüklemede doğrulandı).
        var head = new byte[12];
        var read = await stream.ReadAsync(head, ct);
        stream.Position = 0;
        var type = head[0] == 0x89 ? "image/png" : head[0] == 0xFF ? "image/jpeg" : read >= 12 && head[8] == (byte)'W' ? "image/webp" : "application/octet-stream";
        return new PhotoFile(stream, type);
    }

    // ---------- yardımcılar ----------

    private static bool Kind(string kind) => kind switch
    {
        "photo" => false,
        "cover" => true,
        _ => throw new DomainException("Geçersiz fotoğraf türü."),
    };

    internal static string? NormalizeUsername(string? username) => string.IsNullOrWhiteSpace(username) ? null : username.Trim().ToLowerInvariant();

    private async Task EnsureNumberFreeAsync(string? number, int? exceptUserId, CancellationToken ct)
    {
        var n = Text.CleanOrNull(number);
        if (n is not null && await db.EmployeeProfiles.AnyAsync(p => p.EmployeeNumber == n && p.UserId != exceptUserId, ct))
            throw new ConflictException($"{n} personel numarası başka bir çalışana ait.");
    }

    private async Task EnsureRefsAsync(int? departmentId, int? positionId, int? storeId, CancellationToken ct)
    {
        if (departmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d && x.IsActive, ct)) throw NotFoundException.For("Departman", d);
        if (positionId is { } p && !await db.Positions.AnyAsync(x => x.Id == p, ct)) throw NotFoundException.For("Unvan", p);
        if (storeId is { } s && !await db.Stores.AnyAsync(x => x.Id == s, ct)) throw NotFoundException.For("Mağaza", s);
    }

    /// <summary>Yönetici zinciri döngü oluşturamaz (A → B → A).</summary>
    private async Task EnsureNoCycleAsync(int userId, int managerId, CancellationToken ct)
    {
        if (managerId == userId) throw new DomainException("Bir çalışan kendisinin yöneticisi olamaz.");
        var managers = await db.Users.AsNoTracking().Select(u => new { u.Id, u.ManagerId }).ToDictionaryAsync(x => x.Id, x => x.ManagerId, ct);
        if (!managers.ContainsKey(managerId)) throw NotFoundException.For("Yönetici", managerId);
        var seen = new HashSet<int>();
        for (int? cur = managerId; cur is { } c; cur = managers.GetValueOrDefault(c))
        {
            if (c == userId) throw new DomainException("Bu seçim yönetici zincirinde döngü oluşturur (çalışan, kendi yöneticisinin yöneticisi olamaz).");
            if (!seen.Add(c)) break;
        }
    }
}

public sealed class UpdatePersonalRequestValidator : AbstractValidator<UpdatePersonalRequest>
{
    public UpdatePersonalRequestValidator()
    {
        RuleFor(x => x.Phone).MaximumLength(30).NoMarkup();
        RuleFor(x => x.PersonalEmail).EmailAddress().WithMessage("ad@ornek.com biçiminde bir e-posta girin.").MaximumLength(150).When(x => !string.IsNullOrWhiteSpace(x.PersonalEmail));
        RuleFor(x => x.Address).MaximumLength(300).NoMarkup();
        RuleFor(x => x.BirthDate).Must(d => d is null || (d.Value > new DateOnly(1930, 1, 1) && d.Value < DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-14))))
            .WithMessage("Geçerli bir doğum tarihi girin.");
        RuleFor(x => x.EmergencyContactName).MaximumLength(100).NoMarkup();
        RuleFor(x => x.EmergencyContactPhone).MaximumLength(30).NoMarkup();
        RuleFor(x => x.EmergencyContactRelation).MaximumLength(50).NoMarkup();
    }
}

public sealed class UpdateWorkRequestValidator : AbstractValidator<UpdateWorkRequest>
{
    public UpdateWorkRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.").MaximumLength(100).NoMarkup();
        RuleFor(x => x.EmployeeNumber).MaximumLength(20).NoMarkup();
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.TerminationDate).Must((r, t) => t is null || r.HireDate is null || t >= r.HireDate).WithMessage("Çıkış tarihi işe giriş tarihinden önce olamaz.");
    }
}
