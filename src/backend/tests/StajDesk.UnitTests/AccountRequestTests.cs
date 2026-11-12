using Microsoft.Extensions.Options;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Auth;
using StajDesk.Application.Features.Users;
using StajDesk.Contracts;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>Kapalı portal hesap süreci: İK talep açar, BT/yönetici onaylar; görev ayrılığı ve yetki yükseltme koruması.</summary>
public class AccountRequestTests
{
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly FakeEventPublisher _events = new();
    private readonly NoAudit _audit = new();
    private readonly User _hr, _it, _it2, _admin, _employee;
    private readonly Department _sales;
    private readonly Position _uzman;

    public AccountRequestTests()
    {
        var roles = _db.SeedRoles();
        _sales = new Department { Code = "MG", Name = "Mağazacılık" };
        _uzman = new Position { Title = "Uzman", Level = 7 };
        _db.AddRange(_sales, _uzman);
        User U(string name, string role)
        {
            var u = new User { FullName = name, Email = $"{name}@test" };
            u.Roles.Add(new UserRole { Role = roles[role] });
            return u;
        }
        _hr = U("deniz", RoleNames.Hr);
        _it = U("bt1", RoleNames.ItAdmin);
        _it2 = U("bt2", RoleNames.ItAdmin);
        _admin = U("admin", RoleNames.SystemAdmin);
        _employee = U("ayse", RoleNames.User);
        _db.AddRange(_hr, _it, _it2, _admin, _employee);
        _db.SaveChanges();
    }

    private AccountRequestService Requests() => new(_db, _current, _audit, _events, new NoCache(), Options.Create(new AuthOptions()));
    private UserService Users() => new(_db, new FakeHasher(), _current, new NoCache(), _audit,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<UserService>.Instance, _events, Options.Create(new AuthOptions()));

    private CreateAccountRequest NewHire(string email = "yeni@test", string number = "P9001") =>
        new("Yeni Çalışan", email, number, _sales.Id, _uzman.Id, null, null, EmploymentType.FullTime, new DateOnly(2026, 12, 1), null, null);

    [Fact]
    public async Task Hr_request_then_it_approval_creates_invited_account()
    {
        _current.As(_hr, RoleNames.Hr);
        var id = await Requests().CreateNewAccountAsync(NewHire());
        Assert.Empty(_db.Users.Where(u => u.Email == "yeni@test")); // talep hesap AÇMAZ
        Assert.Contains(_db.Notifications, n => n.UserId == _it.Id);   // BT bilgilendirildi

        _current.As(_it, RoleNames.ItAdmin);
        var done = await Requests().ApproveAsync(id, new ApproveAccountRequest("yeni.calisan", [RoleNames.User], null));

        Assert.Equal(AccountRequestStatus.Approved, done.Status);
        var user = _db.Users.Single(u => u.Email == "yeni@test");
        Assert.Equal(string.Empty, user.PasswordHash);                 // şifreyi çalışan belirler
        Assert.Equal("yeni.calisan", user.Username);
        Assert.Equal(_sales.Id, user.DepartmentId);
        Assert.Equal("P9001", _db.EmployeeProfiles.Single(p => p.UserId == user.Id).EmployeeNumber);
        Assert.Single(_events.Published.Select(p => p.Event).OfType<UserInvitedEvent>());
        Assert.Contains("account_request.approved", _audit.Actions);
    }

    [Fact]
    public async Task Hr_cannot_approve_or_create_accounts_directly()
    {
        _current.As(_hr, RoleNames.Hr);
        var id = await Requests().CreateNewAccountAsync(NewHire());
        await Assert.ThrowsAsync<ForbiddenException>(() => Requests().ApproveAsync(id, new ApproveAccountRequest(null, null, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() => Users().CreateAsync(new CreateUserRequest("X", "x@test", [RoleNames.User], null, null, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() => Users().LockAsync(_employee.Id, new LockUserRequest("deneme")));
    }

    [Fact]
    public async Task Employee_cannot_open_account_requests()
    {
        _current.As(_employee, RoleNames.User);
        await Assert.ThrowsAsync<ForbiddenException>(() => Requests().CreateNewAccountAsync(NewHire()));
    }

    [Fact]
    public async Task It_cannot_grant_roles_beyond_its_own_permissions()
    {
        _current.As(_hr, RoleNames.Hr);
        var id = await Requests().CreateNewAccountAsync(NewHire());
        _current.As(_it, RoleNames.ItAdmin);
        await Assert.ThrowsAsync<ForbiddenException>(() => Requests().ApproveAsync(id, new ApproveAccountRequest(null, [RoleNames.SystemAdmin], null)));
        await Assert.ThrowsAsync<ForbiddenException>(() => Users().UpdateAsync(_employee.Id, new UpdateUserRequest("ayse", [RoleNames.SystemAdmin], null, null, null, true)));
        // BT, sistem yöneticisi hesabını kilitleyemez / değiştiremez.
        await Assert.ThrowsAsync<ForbiddenException>(() => Users().LockAsync(_admin.Id, new LockUserRequest("deneme")));

        _current.As(_admin, RoleNames.SystemAdmin);
        await Requests().ApproveAsync(id, new ApproveAccountRequest(null, [RoleNames.Agent], null)); // yönetici atayabilir
    }

    [Fact]
    public async Task Reviewer_cannot_approve_own_request()
    {
        _current.As(_it, RoleNames.ItAdmin);
        var id = await Requests().CreateNewAccountAsync(NewHire());
        await Assert.ThrowsAsync<ForbiddenException>(() => Requests().ApproveAsync(id, new ApproveAccountRequest(null, null, null)));
        _current.As(_it2, RoleNames.ItAdmin);
        Assert.Equal(AccountRequestStatus.Approved, (await Requests().ApproveAsync(id, new ApproveAccountRequest(null, null, null))).Status);
    }

    [Fact]
    public async Task Rejection_requires_reason_and_notifies_requester()
    {
        _current.As(_hr, RoleNames.Hr);
        var id = await Requests().CreateNewAccountAsync(NewHire());
        Assert.False(new RejectAccountRequestValidator().Validate(new RejectAccountRequest("")).IsValid);

        _current.As(_it, RoleNames.ItAdmin);
        var rejected = await Requests().RejectAsync(id, new RejectAccountRequest("Personel kodu hatalı"));
        Assert.Equal(AccountRequestStatus.Rejected, rejected.Status);
        Assert.Contains(_db.Notifications, n => n.UserId == _hr.Id && n.Message.Contains("Personel kodu hatalı"));
        await Assert.ThrowsAsync<DomainException>(() => Requests().ApproveAsync(id, new ApproveAccountRequest(null, null, null))); // sonuçlanmış
    }

    [Fact]
    public async Task Duplicate_email_or_employee_number_is_rejected()
    {
        _current.As(_hr, RoleNames.Hr);
        await Assert.ThrowsAsync<ConflictException>(() => Requests().CreateNewAccountAsync(NewHire(email: "ayse@test")));
        await Requests().CreateNewAccountAsync(NewHire());
        await Assert.ThrowsAsync<ConflictException>(() => Requests().CreateNewAccountAsync(NewHire()));
        await Assert.ThrowsAsync<ConflictException>(() => Requests().CreateNewAccountAsync(NewHire(email: "baska@test")));
    }

    [Fact]
    public async Task Deactivation_approval_disables_account_and_revokes_sessions()
    {
        _db.RefreshTokens.Add(new RefreshToken { TokenHash = "h", UserId = _employee.Id, ClientId = "web", FamilyId = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddDays(1) });
        _db.SaveChanges();
        _current.As(_hr, RoleNames.Hr);
        var id = await Requests().CreateDeactivationAsync(new CreateDeactivationRequest(_employee.Id, new DateOnly(2026, 11, 30), null));
        Assert.True(_db.Users.Single(u => u.Id == _employee.Id).IsActive);

        _current.As(_it, RoleNames.ItAdmin);
        await Requests().ApproveAsync(id, new ApproveAccountRequest(null, null, null));
        Assert.False(_db.Users.Single(u => u.Id == _employee.Id).IsActive);
        Assert.NotNull(_db.RefreshTokens.Single(t => t.UserId == _employee.Id).RevokedAt);
        Assert.Equal(new DateOnly(2026, 11, 30), _db.EmployeeProfiles.Single(p => p.UserId == _employee.Id).TerminationDate);
    }

    [Fact]
    public async Task Bulk_approve_approves_only_selected_and_reports_failures()
    {
        _current.As(_hr, RoleNames.Hr);
        var a = await Requests().CreateNewAccountAsync(NewHire("a@test", "B1"));
        var b = await Requests().CreateNewAccountAsync(NewHire("b@test", "B2"));
        var notSelected = await Requests().CreateNewAccountAsync(NewHire("c@test", "B3"));
        _current.As(_it, RoleNames.ItAdmin);
        var own = await Requests().CreateNewAccountAsync(NewHire("own@test", "B4")); // görevler ayrılığı: BT kendi talebini onaylayamaz

        var result = await Requests().BulkApproveAsync(new BulkApproveAccountRequests([a, b, own, 999], null));

        Assert.Equal([a, b], result.Approved.Order().ToArray());
        Assert.Equal(2, result.Failed.Count);
        Assert.Contains(result.Failed, f => f.Id == own && f.Reason.Contains("Kendi"));
        Assert.Contains(result.Failed, f => f.Id == 999);
        Assert.Equal(AccountRequestStatus.Approved, _db.AccountRequests.Single(x => x.Id == a).Status);
        Assert.Equal(AccountRequestStatus.Pending, _db.AccountRequests.Single(x => x.Id == notSelected).Status); // seçilmeyen dokunulmaz
        Assert.Equal(AccountRequestStatus.Pending, _db.AccountRequests.Single(x => x.Id == own).Status);
        Assert.True(_db.Users.Any(u => u.Email == "a@test") && _db.Users.Any(u => u.Email == "b@test"));
        Assert.False(_db.Users.Any(u => u.Email == "own@test" || u.Email == "c@test"));
    }

    [Fact]
    public async Task Bulk_approve_requires_account_management_permission_and_a_selection()
    {
        _current.As(_hr, RoleNames.Hr);
        var id = await Requests().CreateNewAccountAsync(NewHire());
        await Assert.ThrowsAsync<ForbiddenException>(() => Requests().BulkApproveAsync(new BulkApproveAccountRequests([id], null)));
        Assert.Equal(AccountRequestStatus.Pending, _db.AccountRequests.Single(x => x.Id == id).Status);

        _current.As(_it, RoleNames.ItAdmin);
        await Assert.ThrowsAsync<DomainException>(() => Requests().BulkApproveAsync(new BulkApproveAccountRequests([], null)));
        await Assert.ThrowsAsync<DomainException>(() => Requests().BulkApproveAsync(new BulkApproveAccountRequests(Enumerable.Range(1, 101).ToList(), null)));
    }

    [Fact]
    public async Task Closing_account_of_employee_who_never_started_does_not_break_date_rule()
    {
        // İşe başlama 1 Aralık, kapatma 12 Kasım: çalışan hiç başlamadı (işe başlamadan iptal).
        _db.EmployeeProfiles.Add(new EmployeeProfile { UserId = _employee.Id, HireDate = new DateOnly(2026, 12, 1) });
        _db.SaveChanges();
        _current.As(_hr, RoleNames.Hr);
        var id = await Requests().CreateDeactivationAsync(new CreateDeactivationRequest(_employee.Id, new DateOnly(2026, 11, 12), null));

        _current.As(_it, RoleNames.ItAdmin);
        await Requests().ApproveAsync(id, new ApproveAccountRequest(null, null, null));

        Assert.False(_db.Users.Single(u => u.Id == _employee.Id).IsActive);
        var profile = _db.EmployeeProfiles.Single(p => p.UserId == _employee.Id);
        Assert.True(profile.TerminationDate >= profile.HireDate); // veritabanı kuralı (ck_employee_profiles_dates) korunur
        Assert.Equal(new DateOnly(2026, 12, 1), profile.TerminationDate);
    }

    [Fact]
    public async Task It_locks_and_unlocks_account_with_audit()
    {
        _db.RefreshTokens.Add(new RefreshToken { TokenHash = "h2", UserId = _employee.Id, ClientId = "web", FamilyId = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddDays(1) });
        _db.SaveChanges();
        _current.As(_it, RoleNames.ItAdmin);
        await Users().LockAsync(_employee.Id, new LockUserRequest("Şüpheli giriş"));
        Assert.NotNull(_db.Users.Single(u => u.Id == _employee.Id).LockedAt);
        Assert.NotNull(_db.RefreshTokens.Single(t => t.UserId == _employee.Id).RevokedAt);
        await Users().UnlockAsync(_employee.Id);
        Assert.Null(_db.Users.Single(u => u.Id == _employee.Id).LockedAt);
        Assert.Contains("user.locked", _audit.Actions);
        Assert.Contains("user.unlocked", _audit.Actions);
    }
}
