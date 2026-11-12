using StajDesk.Application.Common;
using StajDesk.Application.Features.Leave;
using StajDesk.Application.Features.Workflows;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>Aşama 6: onay motoru (yönetici → izin yöneticisi), izin bakiyesi, yetkiler ve kenar durumlar.</summary>
public class WorkflowLeaveTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc); // Perşembe
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly User _employee, _manager, _director, _leaveAdmin, _hr, _admin;
    private readonly LeaveType _annual, _unpaid;

    private sealed class FixedClock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    public WorkflowLeaveTests()
    {
        var roles = _db.SeedRoles();
        User U(string name, string role)
        {
            var u = new User { FullName = name, Email = $"{name}@test" };
            u.Roles.Add(new UserRole { Role = roles[role] });
            return u;
        }
        _director = U("Direktör", RoleNames.User);
        _manager = U("Müdür", RoleNames.User);
        _employee = U("Çalışan", RoleNames.User);
        _leaveAdmin = U("İzinci", RoleNames.LeaveAdmin);
        _hr = U("İK", RoleNames.Hr);
        _admin = U("Admin", RoleNames.SystemAdmin);
        _db.AddRange(_director, _manager, _employee, _leaveAdmin, _hr, _admin);
        _db.SaveChanges();
        _manager.ManagerId = _director.Id;
        _employee.ManagerId = _manager.Id;

        var def = new WorkflowDefinition { Key = WorkflowKeys.Leave, Name = "İzin talebi" };
        def.Steps.Add(new WorkflowStepDefinition { Order = 1, Name = "Yönetici onayı", ApproverType = ApproverType.Manager });
        def.Steps.Add(new WorkflowStepDefinition { Order = 2, Name = "İzin yöneticisi onayı", ApproverType = ApproverType.Role, RoleName = RoleNames.LeaveAdmin });
        _annual = new LeaveType { Code = "YILLIK", Name = "Yıllık izin", TracksBalance = true, DefaultDaysPerYear = 14 };
        _unpaid = new LeaveType { Code = "UCRETSIZ", Name = "Ücretsiz izin", IsPaid = false };
        _db.AddRange(def, _annual, _unpaid);
        _db.SaveChanges();
    }

    private WorkflowEngine Engine() => new(_db, _current, new NoAudit(), [new LeaveWorkflowHandler(_db)]);
    private LeaveService Leave() => new(_db, _current, new NoAudit(), Engine(), new FixedClock(Now));
    private static DateOnly D(int month, int day) => new(2026, month, day);

    private async Task<LeaveRequestDto> Request(DateOnly start, DateOnly end, LeaveType? type = null)
    {
        _current.As(_employee, RoleNames.User);
        return await Leave().CreateAsync(new CreateLeaveRequest((type ?? _annual).Id, start, end, "Tatil"));
    }

    private async Task<int> MyApprovalId(User approver, string role)
    {
        _current.As(approver, role);
        var inbox = await Engine().InboxAsync(new Application.Features.Admin.SimplePagedQuery());
        return Assert.Single(inbox.Items).ApprovalId;
    }

    [Fact]
    public async Task Leave_goes_manager_then_leave_admin_and_updates_balance()
    {
        var req = await Request(D(11, 16), D(11, 18)); // Pzt–Çar: 3 iş günü
        Assert.Equal(3, req.Days);
        Assert.Equal(LeaveStatus.Pending, req.Status);

        // İzin yöneticisi sırası gelmeden karar veremez; onay kutusunda da görmez
        _current.As(_leaveAdmin, RoleNames.LeaveAdmin);
        Assert.Empty((await Engine().InboxAsync(new Application.Features.Admin.SimplePagedQuery())).Items);

        var step1 = await MyApprovalId(_manager, RoleNames.User);
        _current.As(_leaveAdmin, RoleNames.LeaveAdmin);
        await Assert.ThrowsAsync<ForbiddenException>(() => Engine().DecideAsync(step1, true, null));
        _current.As(_manager, RoleNames.User);
        var after1 = await Engine().DecideAsync(step1, true, "Uygundur");
        Assert.Equal(2, after1.Steps.Single(s => s.IsCurrent).StepOrder);

        // Rolü olmayan İK karar veremez (İK ≠ izin yönetimi)
        var step2 = await MyApprovalId(_leaveAdmin, RoleNames.LeaveAdmin);
        _current.As(_hr, RoleNames.Hr);
        await Assert.ThrowsAsync<ForbiddenException>(() => Engine().DecideAsync(step2, true, null));
        _current.As(_leaveAdmin, RoleNames.LeaveAdmin);
        var done = await Engine().DecideAsync(step2, true, null);

        Assert.Equal(WorkflowStatus.Approved, done.Status);
        Assert.Equal(LeaveStatus.Approved, _db.LeaveRequests.Single().Status);
        _current.As(_employee, RoleNames.User);
        var balance = (await Leave().BalancesAsync(null, 2026)).Single(b => b.LeaveTypeId == _annual.Id);
        Assert.Equal((14m, 3m, 0m, 11m), (balance.Entitled, balance.Used, balance.Pending, balance.Remaining));
        Assert.Contains(_db.Notifications, n => n.UserId == _employee.Id && n.Title.Contains("onaylandı") && n.Link == $"/leave?request={req.Id}");
    }

    [Fact]
    public async Task Rejection_needs_a_reason_and_releases_pending_days()
    {
        await Request(D(11, 16), D(11, 20)); // 5 gün
        var step1 = await MyApprovalId(_manager, RoleNames.User);
        await Assert.ThrowsAsync<DomainException>(() => Engine().DecideAsync(step1, false, "  "));
        var res = await Engine().DecideAsync(step1, false, "Yoğun dönem");

        Assert.Equal(WorkflowStatus.Rejected, res.Status);
        Assert.Equal(ApprovalDecision.Skipped, res.Steps[1].Decision); // sonraki adım çalışmaz
        Assert.Equal(LeaveStatus.Rejected, _db.LeaveRequests.Single().Status);
        _current.As(_employee, RoleNames.User);
        Assert.Equal(14, (await Leave().BalancesAsync(null, 2026)).Single().Remaining);
        Assert.Contains(_db.Notifications, n => n.UserId == _employee.Id && n.Message.Contains("Yoğun dönem"));
    }

    [Fact]
    public async Task Requester_cannot_approve_own_request_even_with_the_step_role()
    {
        _current.As(_leaveAdmin, RoleNames.LeaveAdmin);
        await Leave().CreateAsync(new CreateLeaveRequest(_unpaid.Id, D(11, 16), D(11, 16), null));
        // Yöneticisi yok → yönetici adımı atlandı, sıra izin yöneticisi rolünde; ama kendi talebi onay kutusunda çıkmaz
        Assert.Empty((await Engine().InboxAsync(new Application.Features.Admin.SimplePagedQuery())).Items);
        var approval = _db.WorkflowApprovals.Single(a => a.StepOrder == 2);
        await Assert.ThrowsAsync<ForbiddenException>(() => Engine().DecideAsync(approval.Id, true, null));
        Assert.Equal(ApprovalDecision.Skipped, _db.WorkflowApprovals.Single(a => a.StepOrder == 1).Decision);
    }

    [Fact]
    public async Task Same_person_on_consecutive_steps_approves_once()
    {
        // Müdür aynı zamanda departman yöneticisi: "yönetici → departman yöneticisi" zincirinde tek onay yeter
        var dept = new Department { Code = "MG", Name = "Mağazacılık", ManagerId = _manager.Id };
        _db.Departments.Add(dept);
        _db.SaveChanges();
        _employee.DepartmentId = dept.Id;
        _db.SaveChanges();
        _current.As(_admin, RoleNames.SystemAdmin);
        var def = _db.WorkflowDefinitions.Single();
        await Engine().SaveDefinitionAsync(def.Id, new SaveWorkflowRequest("İzin talebi", null, true,
            [new("Yönetici onayı", ApproverType.Manager, null, null, null), new("Departman onayı", ApproverType.DepartmentManager, null, null, null)]));

        await Request(D(11, 16), D(11, 16));
        var step1 = await MyApprovalId(_manager, RoleNames.User);
        var res = await Engine().DecideAsync(step1, true, null);

        Assert.Equal(WorkflowStatus.Approved, res.Status);
        Assert.Equal(ApprovalDecision.Skipped, res.Steps[1].Decision);
    }

    [Fact]
    public async Task Second_level_manager_step_routes_to_managers_manager()
    {
        _current.As(_admin, RoleNames.SystemAdmin);
        var def = _db.WorkflowDefinitions.Single();
        await Engine().SaveDefinitionAsync(def.Id, new SaveWorkflowRequest("İzin talebi", null, true,
            [new("Yönetici", ApproverType.Manager, null, null, null), new("Müdür onayı", ApproverType.SecondLevelManager, null, null, null)]));
        await Request(D(11, 16), D(11, 16));
        await Engine().DecideAsync(await MyApprovalId(_manager, RoleNames.User), true, null);
        var step2 = await MyApprovalId(_director, RoleNames.User);
        Assert.Equal(WorkflowStatus.Approved, (await Engine().DecideAsync(step2, true, null)).Status);
    }

    [Fact]
    public async Task Balance_overlap_and_working_day_rules_are_enforced()
    {
        await Assert.ThrowsAsync<DomainException>(() => Request(D(11, 16), D(12, 4)));  // 15 iş günü > 14
        await Assert.ThrowsAsync<DomainException>(() => Request(D(11, 14), D(11, 15))); // hafta sonu
        await Request(D(11, 16), D(11, 17));
        await Assert.ThrowsAsync<ConflictException>(() => Request(D(11, 17), D(11, 19))); // çakışma
        await Assert.ThrowsAsync<DomainException>(() => Request(D(9, 1), D(9, 1)));   // 30 günden eski

        // Kişiye özel hak tanımlanınca uzun izin mümkün; yalnızca izin yöneticisi tanımlar
        _current.As(_hr, RoleNames.Hr);
        await Assert.ThrowsAsync<ForbiddenException>(() => Leave().SetEntitlementAsync(new SetEntitlementRequest(_employee.Id, _annual.Id, 2026, 20, null)));
        _current.As(_leaveAdmin, RoleNames.LeaveAdmin);
        await Leave().SetEntitlementAsync(new SetEntitlementRequest(_employee.Id, _annual.Id, 2026, 20, "Kıdem"));
        var b = (await Leave().BalancesAsync(_employee.Id, 2026)).Single();
        Assert.Equal((20m, 2m, 18m, true), (b.Entitled, b.Pending, b.Remaining, b.IsCustom));
    }

    [Fact]
    public void Working_days_skip_weekends_and_fixed_public_holidays()
    {
        var (days, holidays) = WorkingDays.Count(D(10, 26), D(11, 1)); // Pzt–Paz, 29 Ekim Perşembe
        Assert.Equal(4, days);
        Assert.Equal([D(10, 29)], holidays);
    }

    [Fact]
    public async Task Cancelling_a_pending_request_closes_its_workflow()
    {
        var req = await Request(D(11, 16), D(11, 16));
        await Leave().CancelAsync(req.Id);
        Assert.Equal(LeaveStatus.Cancelled, _db.LeaveRequests.Single().Status);
        Assert.Equal(WorkflowStatus.Cancelled, _db.WorkflowInstances.Single().Status);
        _current.As(_manager, RoleNames.User);
        Assert.Empty((await Engine().InboxAsync(new Application.Features.Admin.SimplePagedQuery())).Items);
    }

    [Fact]
    public async Task Others_cannot_see_someone_elses_leave_or_balance()
    {
        var req = await Request(D(11, 16), D(11, 16));
        _current.As(_hr, RoleNames.Hr);
        await Assert.ThrowsAsync<NotFoundException>(() => Leave().GetAsync(req.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => Leave().BalancesAsync(_employee.Id, 2026));
        _current.As(_manager, RoleNames.User); // onay zincirindeki yönetici görebilir
        Assert.Equal(req.Id, (await Leave().GetAsync(req.Id)).Id);
        _current.As(_leaveAdmin, RoleNames.LeaveAdmin);
        Assert.Equal(req.Id, (await Leave().GetAsync(req.Id)).Id);
    }

    [Fact]
    public async Task Only_workflow_managers_edit_chains_and_roles_must_exist()
    {
        var id = _db.WorkflowDefinitions.Single().Id;
        _current.As(_leaveAdmin, RoleNames.LeaveAdmin);
        await Assert.ThrowsAsync<ForbiddenException>(() => Engine().SaveDefinitionAsync(id, new SaveWorkflowRequest("x", null, true, [])));
        _current.As(_admin, RoleNames.SystemAdmin);
        await Assert.ThrowsAsync<DomainException>(() => Engine().SaveDefinitionAsync(id, new SaveWorkflowRequest("x", null, true,
            [new("Adım", ApproverType.Role, "olmayan_rol", null, null)])));
        // Zincir boşaltılırsa yeni talepler hemen onaylanır
        await Engine().SaveDefinitionAsync(id, new SaveWorkflowRequest("İzin talebi", null, true, []));
        var req = await Request(D(11, 16), D(11, 16));
        Assert.Equal(LeaveStatus.Approved, req.Status);
    }
}
