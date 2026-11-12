using Microsoft.Extensions.Options;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Auth;
using StajDesk.Application.Features.Employees;
using StajDesk.Contracts;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>Organizasyon ve personel: kim neyi görür, kim neyi değiştirir; unvan ≠ rol; döngü kontrolleri.</summary>
public class OrganizationTests
{
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly FakeEventPublisher _events = new();
    private readonly NoAudit _audit = new();
    private readonly User _hr, _manager, _employee, _colleague, _admin;
    private readonly Department _it, _sales;
    private readonly Position _mudur, _uzman;

    public OrganizationTests()
    {
        var roles = _db.SeedRoles();
        _it = new Department { Code = "BT", Name = "Bilgi Teknolojileri" };
        _sales = new Department { Code = "MG", Name = "Mağazacılık" };
        _mudur = new Position { Title = "Müdür", Level = 4 };
        _uzman = new Position { Title = "Uzman", Level = 7 };
        _db.AddRange(_it, _sales, _mudur, _uzman);
        _db.SaveChanges();

        User U(string name, string role, Position? pos = null, Department? dept = null)
        {
            var u = new User { FullName = name, Email = $"{name}@test", Position = pos, Department = dept, Profile = new EmployeeProfile { BirthDate = new DateOnly(1995, 4, 12), Address = "Gizli adres" } };
            u.Roles.Add(new UserRole { Role = roles[role] });
            return u;
        }
        _hr = U("deniz", RoleNames.Hr);
        // Unvanı "Müdür" ama sistem rolü yalnızca Kullanıcı: unvan yetki vermez.
        _manager = U("burak", RoleNames.User, _mudur, _it);
        _employee = U("elif", RoleNames.User, _uzman, _it);
        _colleague = U("ayse", RoleNames.User, _uzman, _sales);
        _admin = U("admin", RoleNames.SystemAdmin);
        _db.AddRange(_hr, _manager, _employee, _colleague, _admin);
        _db.SaveChanges();
        _employee.ManagerId = _manager.Id;
        _db.SaveChanges();
    }

    private EmployeeService Employees() => new(_db, _current, new NoCache(), _audit, _events, new MemoryStorage(), Options.Create(new AuthOptions()));
    private OrgService Org() => new(_db, _current, _audit);

    private UpdateWorkRequest Work(User u, int? managerId = null, bool active = true) =>
        new(u.FullName, null, null, u.DepartmentId, u.PositionId, managerId, null, null, null, EmploymentType.FullTime, active);

    [Fact]
    public async Task Colleague_sees_public_profile_but_not_private_fields()
    {
        _current.As(_colleague, RoleNames.User);
        var p = await Employees().GetAsync(_employee.Id);
        Assert.Equal("Uzman", p.Position!.Name);
        Assert.Equal("burak", p.Manager!.FullName);
        Assert.Null(p.Private);
        Assert.Equal("04-12", p.Birthday); // doğum günü (ay-gün) görünür, yıl görünmez
        Assert.False(p.Access.CanEditPersonal);
    }

    [Fact]
    public async Task Self_and_hr_see_private_fields()
    {
        _current.As(_employee, RoleNames.User);
        Assert.Equal("Gizli adres", (await Employees().GetAsync(_employee.Id)).Private!.Address);
        _current.As(_hr, RoleNames.Hr);
        Assert.Equal(new DateOnly(1995, 4, 12), (await Employees().GetAsync(_employee.Id)).Private!.BirthDate);
    }

    [Fact]
    public async Task Employee_edits_own_personal_info_but_not_work_info()
    {
        _current.As(_employee, RoleNames.User);
        await Employees().UpdatePersonalAsync(_employee.Id, new UpdatePersonalRequest("0555", null, "Yeni adres", null, "Annem", "0532", "Anne"));
        Assert.Equal("Yeni adres", _db.EmployeeProfiles.Single(p => p.UserId == _employee.Id).Address);

        await Assert.ThrowsAsync<ForbiddenException>(() => Employees().UpdateWorkAsync(_employee.Id, Work(_employee)));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Employees().UpdatePersonalAsync(_colleague.Id, new UpdatePersonalRequest(null, null, "x", null, null, null, null)));
    }

    [Fact]
    public async Task Hr_manages_personnel_but_cannot_manage_roles_or_organization()
    {
        _current.As(_hr, RoleNames.Hr);
        await Employees().UpdateWorkAsync(_colleague.Id, Work(_colleague, managerId: _manager.Id));
        Assert.Equal(_manager.Id, _db.Users.Single(u => u.Id == _colleague.Id).ManagerId);

        // İK rolü sistem rolü/izin yönetimi ve departman yönetimi vermez.
        Assert.DoesNotContain(Permissions.UsersManage, _current.Permissions);
        Assert.DoesNotContain(Permissions.RolesManage, _current.Permissions);
        await Assert.ThrowsAsync<ForbiddenException>(() => Org().SaveDepartmentAsync(null, new UpsertDepartmentRequest("YN", "Yeni", null, null, null)));
    }

    [Fact]
    public async Task Hr_creates_employee_with_invitation_and_user_role()
    {
        _current.As(_hr, RoleNames.Hr);
        var id = await Employees().CreateAsync(new CreateEmployeeRequest("Yeni Çalışan", "Yeni@Test", "yeni.calisan", null, "P9001", _sales.Id, _uzman.Id, _manager.Id, null, new DateOnly(2026, 11, 1)));
        var u = _db.Users.Single(x => x.Id == id);
        Assert.Equal(string.Empty, u.PasswordHash);                // şifre davetle belirlenir
        Assert.Equal("yeni.calisan", u.Username);
        Assert.Single(_events.Published.Select(p => p.Event).OfType<UserInvitedEvent>());
        Assert.Equal(RoleNames.User, _db.UserRoles.Where(r => r.UserId == id).Select(r => r.Role.Name).Single());
        await Assert.ThrowsAsync<ConflictException>(() =>
            Employees().CreateAsync(new CreateEmployeeRequest("Başka", "baska@test", null, null, "P9001", null, null, null, null, null)));
    }

    [Fact]
    public async Task Manager_chain_cannot_form_a_cycle()
    {
        _current.As(_hr, RoleNames.Hr);
        // elif → burak; şimdi burak'ın yöneticisi elif olamaz.
        await Assert.ThrowsAsync<DomainException>(() => Employees().UpdateWorkAsync(_manager.Id, Work(_manager, managerId: _employee.Id)));
        await Assert.ThrowsAsync<DomainException>(() => Employees().UpdateWorkAsync(_manager.Id, Work(_manager, managerId: _manager.Id)));
    }

    [Fact]
    public async Task Deactivating_employee_revokes_sessions_and_hides_from_directory()
    {
        _db.RefreshTokens.Add(new RefreshToken { TokenHash = "h", UserId = _colleague.Id, ClientId = "web", FamilyId = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddDays(1) });
        _db.SaveChanges();
        _current.As(_hr, RoleNames.Hr);
        await Employees().UpdateWorkAsync(_colleague.Id, Work(_colleague, active: false));
        Assert.NotNull(_db.RefreshTokens.Single(t => t.UserId == _colleague.Id).RevokedAt);

        _current.As(_employee, RoleNames.User);
        var list = await Employees().ListAsync(new EmployeeQuery { IncludeInactive = true }); // İK değil: yok sayılır
        Assert.DoesNotContain(list.Items, e => e.Id == _colleague.Id);
    }

    [Fact]
    public async Task Department_tree_rejects_cycles_and_delete_with_members()
    {
        _current.As(_admin, RoleNames.SystemAdmin);
        var child = await Org().SaveDepartmentAsync(null, new UpsertDepartmentRequest("dst", "Destek", null, _it.Id, null));
        Assert.Equal("DST", child.Code);
        await Assert.ThrowsAsync<DomainException>(() => Org().SaveDepartmentAsync(_it.Id, new UpsertDepartmentRequest("BT", "Bilgi Teknolojileri", null, child.Id, null)));
        await Assert.ThrowsAsync<DomainException>(() => Org().DeleteDepartmentAsync(_it.Id)); // alt departmanı ve çalışanı var
        await Assert.ThrowsAsync<DomainException>(() => Org().DeletePositionAsync(_uzman.Id)); // kullanılan unvan
    }

    [Fact]
    public async Task Org_chart_lists_active_people_with_manager_links()
    {
        _current.As(_colleague, RoleNames.User);
        var chart = await Org().GetChartAsync();
        Assert.Equal(_manager.Id, chart.People.Single(p => p.Id == _employee.Id).ManagerId);
        Assert.Contains(chart.Departments, d => d.Code == "BT" && d.MemberCount == 2);
    }
}
