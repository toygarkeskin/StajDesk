using System.Text;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>CSV ile toplu çalışan ekleme: hesap açmaz, talep açar; doğrulama, tekrar ve yetki kuralları.</summary>
public class AccountImportTests
{
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly User _admin, _hr, _it, _employee;

    private const string Header = "Ad Soyad;E-posta;Personel Kodu;Departman;Unvan;Personel Türü;İşe Başlama;Telefon;Not\r\n";

    public AccountImportTests()
    {
        var roles = _db.SeedRoles();
        User U(string name, string role)
        {
            var u = new User { FullName = name, Email = $"{name}@test" };
            u.Roles.Add(new UserRole { Role = roles[role] });
            return u;
        }
        _admin = U("admin", RoleNames.SystemAdmin);
        _hr = U("deniz", RoleNames.Hr);
        _it = U("bt", RoleNames.ItAdmin);
        _employee = U("ayse", RoleNames.User);
        _db.AddRange(_admin, _hr, _it, _employee);
        _db.Departments.Add(new Department { Code = "MG", Name = "Mağazacılık" });
        _db.Positions.Add(new Position { Title = "Uzman", Level = 7 });
        _db.SaveChanges();
    }

    private AccountImportService Service() => new(_db, _current, new NoAudit());
    private static MemoryStream Csv(string body) => new(Encoding.UTF8.GetBytes(Header + body));

    [Fact]
    public async Task Dry_run_validates_without_saving_anything()
    {
        _current.As(_admin, RoleNames.SystemAdmin);
        var result = await Service().ImportAsync(Csv("Yeni Kişi;yeni@test;P1;Mağazacılık;Uzman;Tam zamanlı;01.12.2026;;\r\n"), dryRun: true);

        Assert.True(result.DryRun);
        Assert.Equal(1, result.ValidCount);
        Assert.Empty(_db.AccountRequests);
        Assert.Empty(_db.Users.Where(u => u.Email == "yeni@test"));
    }

    [Fact]
    public async Task Import_creates_pending_requests_not_accounts_and_notifies_it()
    {
        _current.As(_hr, RoleNames.Hr);
        var result = await Service().ImportAsync(Csv(
            "Ayşe Yılmaz;AYSE.Y@test;P10;MG;uzman;Stajyer;2026-12-01;;\r\n" +
            "Mehmet Demir;mehmet@test;P11;mağazacılık;Uzman;;15.12.2026;0532;\"Not; ayırıcılı\"\r\n"), dryRun: false);

        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(2, _db.AccountRequests.Count(r => r.Status == AccountRequestStatus.Pending && r.Type == AccountRequestType.NewAccount));
        Assert.DoesNotContain(_db.Users, u => u.Email == "ayse.y@test"); // hesap AÇILMAZ, BT onayı beklenir
        var ayse = _db.AccountRequests.Single(r => r.Email == "ayse.y@test");
        Assert.Equal(EmploymentType.Intern, ayse.EmploymentType);
        Assert.Equal(new DateOnly(2026, 12, 1), ayse.HireDate);
        Assert.Equal("Not; ayırıcılı", _db.AccountRequests.Single(r => r.Email == "mehmet@test").Note);
        Assert.Contains(_db.Notifications, n => n.UserId == _it.Id); // tek toplu bildirim
        Assert.Single(_db.Notifications, n => n.UserId == _it.Id);
    }

    [Fact]
    public async Task Invalid_and_duplicate_rows_are_reported_and_skipped()
    {
        _db.Users.Add(new User { FullName = "Var", Email = "var@test" });
        _db.SaveChanges();
        _current.As(_admin, RoleNames.SystemAdmin);

        var result = await Service().ImportAsync(Csv(
            "Geçerli;ok@test;P1;MG;Uzman;;01.12.2026;;\r\n" +
            "Mevcut;var@test;P2;MG;Uzman;;01.12.2026;;\r\n" +
            "Tekrar;ok@test;P3;MG;Uzman;;01.12.2026;;\r\n" +
            "Kodsuz;kodsuz@test;;MG;Uzman;;01.12.2026;;\r\n" +
            "Tarih;tarih@test;P5;MG;Uzman;;31.02.2026;;\r\n" +
            "<b>Html</b>;html@test;P6;MG;Uzman;;01.12.2026;;\r\n" +
            "Tür;tur@test;P7;MG;Uzman;Haftalık;01.12.2026;;\r\n"), dryRun: false);

        Assert.Equal(7, result.Total);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal("ok@test", Assert.Single(_db.AccountRequests).Email);
        Assert.All(result.Rows.Skip(1), r => Assert.False(r.Valid));
        Assert.Equal(2, result.Rows[0].Line); // başlık dosyanın 1. satırı; hata mesajı Excel satır numarasıyla eşleşir
        Assert.Contains(result.Rows[2].Errors, e => e.Contains("birden fazla"));
    }

    [Fact]
    public async Task Missing_department_and_title_are_created_only_with_org_permission()
    {
        // İK: org.manage yok → bilinmeyen departman hata
        _current.As(_hr, RoleNames.Hr);
        _current.Permissions = new HashSet<string>(_current.Permissions.Where(p => p != Permissions.OrgManage));
        var hr = await Service().ImportAsync(Csv("A B;ab@test;P1;Satış Operasyonları;Bölge Müdürü;;01.12.2026;;\r\n"), dryRun: true);
        Assert.False(hr.Rows[0].Valid);
        Assert.Empty(hr.NewDepartments);

        // Yönetici: departman ve unvan oluşturulur
        _current.As(_admin, RoleNames.SystemAdmin);
        var admin = await Service().ImportAsync(Csv("A B;ab@test;P1;Satış Operasyonları;Bölge Müdürü;;01.12.2026;;\r\n"), dryRun: false);
        Assert.Equal(["Satış Operasyonları"], admin.NewDepartments);
        Assert.Equal(["Bölge Müdürü"], admin.NewPositions);
        var dept = _db.Departments.Single(d => d.Name == "Satış Operasyonları");
        Assert.Equal("SO", dept.Code);
        Assert.Equal(dept.Id, _db.AccountRequests.Single().DepartmentId);
    }

    [Fact]
    public async Task Employees_without_request_permission_cannot_import()
    {
        _current.As(_employee, RoleNames.User);
        await Assert.ThrowsAsync<ForbiddenException>(() => Service().ImportAsync(Csv("A;a@test;P1;MG;Uzman;;01.12.2026;;\r\n"), dryRun: true));
    }

    [Fact]
    public async Task Missing_required_columns_are_rejected()
    {
        _current.As(_admin, RoleNames.SystemAdmin);
        var csv = new MemoryStream(Encoding.UTF8.GetBytes("İsim,Mail\r\nA,a@test\r\n"));
        await Assert.ThrowsAsync<DomainException>(() => Service().ImportAsync(csv, dryRun: true));
    }

    [Fact]
    public void Parser_handles_quotes_commas_and_newlines()
    {
        var rows = AccountImportService.Parse("a,\"b, c\",\"d \"\"x\"\"\"\r\n\"çok\nsatır\",e,f\n", ',');
        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b, c", "d \"x\""], rows[0]);
        Assert.Equal("çok\nsatır", rows[1][0]);
    }

    [Fact]
    public async Task Windows_1254_encoded_file_is_read()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _current.As(_admin, RoleNames.SystemAdmin);
        var bytes = Encoding.GetEncoding(1254).GetBytes(Header + "Şükrü Işık;sukru@test;P9;Mağazacılık;Uzman;;01.12.2026;;\r\n");
        var result = await Service().ImportAsync(new MemoryStream(bytes), dryRun: true);
        Assert.True(result.Rows[0].Valid);
        Assert.Equal("Şükrü Işık", result.Rows[0].FullName);
    }
}
