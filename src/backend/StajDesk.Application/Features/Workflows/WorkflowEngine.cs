using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Workflows;

/* =========================================================
   Onay (iş akışı) motoru.
   - Akış başlarken tanımdaki her adım için bir onay kaydı oluşturulur ve onaylayıcı ÇÖZÜLÜR (anlık görüntü):
     sonradan yönetici değişse de yarım kalmış akış tutarlı kalır.
   - Onaylayıcısı olmayan, talep sahibinin kendisine düşen veya bir önceki adımı aynı kişinin onayladığı adım atlanır.
   - Karar yalnızca adımın onaylayıcısı (kişi) ya da adımın rolüne sahip biri tarafından verilir; talep sahibi kendi talebini onaylayamaz.
   - Akış bitince bağlı kaydın sahibi (ör. izin) IWorkflowSubjectHandler ile haberdar edilir.
   ========================================================= */

/// <summary>Akış sonucunu bağlı kayda uygular (ör. izin talebinin durumunu günceller). Konu türü başına bir tane.</summary>
public interface IWorkflowSubjectHandler
{
    string SubjectType { get; }
    /// <summary>Kaydetme çağıranın işidir (aynı birim içinde).</summary>
    Task OnCompletedAsync(WorkflowInstance instance, WorkflowStatus status, string? comment, CancellationToken ct);
    /// <summary>Onay kutusunda gösterilecek kısa özet (ör. "Yıllık izin · 3 gün · 12–14 Kas").</summary>
    Task<IReadOnlyDictionary<int, string>> DescribeAsync(IReadOnlyCollection<int> subjectIds, CancellationToken ct);
    /// <summary>Kaydın ayrıntı sayfası (ör. /leave?request=5)</summary>
    string LinkFor(int subjectId);
}

public static class WorkflowKeys
{
    public const string Leave = "leave";
}

public sealed record WorkflowStepDto(int Id, int Order, string Name, ApproverType ApproverType, string? RoleName, int? UserId, string? UserName, int? DepartmentId, string? DepartmentName);
public sealed record WorkflowDefinitionDto(int Id, string Key, string Name, string? Description, bool IsActive, IReadOnlyList<WorkflowStepDto> Steps);
public sealed record SaveWorkflowStepRequest(string Name, ApproverType ApproverType, string? RoleName, int? UserId, int? DepartmentId);
public sealed record SaveWorkflowRequest(string Name, string? Description, bool IsActive, IReadOnlyList<SaveWorkflowStepRequest> Steps);

public sealed record ApprovalStepDto(int Id, int StepOrder, string StepName, string? ApproverName, string? ApproverRole, ApprovalDecision Decision,
    string? DecidedByName, DateTime? DecidedAt, string? Comment, bool IsCurrent);
public sealed record WorkflowInstanceDto(int Id, string Key, string WorkflowName, string Title, string SubjectType, int SubjectId, string? Link,
    int RequesterId, string RequesterName, WorkflowStatus Status, DateTime CreatedAt, DateTime? CompletedAt, IReadOnlyList<ApprovalStepDto> Steps, bool CanDecide, int? MyApprovalId);
public sealed record ApprovalInboxItemDto(int ApprovalId, int InstanceId, string WorkflowName, string Title, string? Summary, string? Link, string StepName,
    int RequesterId, string RequesterName, string? RequesterDepartment, DateTime CreatedAt);
public sealed record DecideRequest(string? Comment);
public sealed record RoleOptionDto(string Name, string DisplayName);

public interface IWorkflowEngine
{
    /// <summary>Akışı başlatır. Tanım yoksa/pasifse veya hiç onaylayıcı yoksa akış hemen onaylanır.</summary>
    Task<WorkflowInstance> StartAsync(string key, string subjectType, int subjectId, string title, int requesterId, CancellationToken ct = default);
    Task CancelAsync(string subjectType, int subjectId, CancellationToken ct = default);
}

public interface IWorkflowService
{
    Task<IReadOnlyList<WorkflowDefinitionDto>> DefinitionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RoleOptionDto>> RoleOptionsAsync(CancellationToken ct = default);
    Task<WorkflowDefinitionDto> SaveDefinitionAsync(int id, SaveWorkflowRequest request, CancellationToken ct = default);
    Task<PagedResult<ApprovalInboxItemDto>> InboxAsync(PagedQuery query, CancellationToken ct = default);
    Task<int> InboxCountAsync(CancellationToken ct = default);
    Task<WorkflowInstanceDto> GetAsync(int instanceId, CancellationToken ct = default);
    Task<WorkflowInstanceDto?> FindBySubjectAsync(string subjectType, int subjectId, CancellationToken ct = default);
    Task<WorkflowInstanceDto> DecideAsync(int approvalId, bool approve, string? comment, CancellationToken ct = default);
}

internal sealed class WorkflowEngine(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, IEnumerable<IWorkflowSubjectHandler> handlers)
    : IWorkflowEngine, IWorkflowService
{
    private IWorkflowSubjectHandler? Handler(string subjectType) => handlers.FirstOrDefault(h => h.SubjectType == subjectType);

    private void Notify(int userId, string title, string message, string? link) =>
        db.Notifications.Add(new Notification { UserId = userId, Title = title.Length > 200 ? title[..199] : title, Message = message.Length > 1000 ? message[..999] : message, Link = link });

    // ---------- Başlatma ve ilerletme ----------

    public async Task<WorkflowInstance> StartAsync(string key, string subjectType, int subjectId, string title, int requesterId, CancellationToken ct = default)
    {
        var def = await db.WorkflowDefinitions.Include(d => d.Steps).FirstOrDefaultAsync(d => d.Key == key, ct)
                  ?? throw new DomainException($"\"{key}\" iş akışı tanımlı değil.");
        var requester = await db.Users.AsNoTracking().Where(u => u.Id == requesterId)
            .Select(u => new { u.Id, u.ManagerId, u.DepartmentId, ManagersManager = u.Manager == null ? null : u.Manager.ManagerId,
                DepartmentManager = u.Department == null ? null : u.Department.ManagerId })
            .FirstAsync(ct);

        var instance = new WorkflowInstance { WorkflowDefinitionId = def.Id, SubjectType = subjectType, SubjectId = subjectId, Title = title, RequesterId = requesterId };
        db.WorkflowInstances.Add(instance);
        if (def.IsActive)
        {
            foreach (var step in def.Steps.OrderBy(s => s.Order))
            {
                int? approver = null;
                string? role = null;
                switch (step.ApproverType)
                {
                    case ApproverType.Manager: approver = requester.ManagerId; break;
                    case ApproverType.SecondLevelManager: approver = requester.ManagersManager; break;
                    case ApproverType.DepartmentManager:
                        approver = step.DepartmentId is { } d
                            ? await db.Departments.Where(x => x.Id == d).Select(x => x.ManagerId).FirstOrDefaultAsync(ct)
                            : requester.DepartmentManager;
                        break;
                    case ApproverType.User: approver = step.UserId; break;
                    case ApproverType.Role: role = step.RoleName; break;
                }
                // Pasif hesap onaylayamaz
                if (approver is { } a && !await db.Users.AnyAsync(u => u.Id == a && u.IsActive, ct)) approver = null;
                var skip = (approver is null && role is null) || approver == requesterId;
                instance.Approvals.Add(new WorkflowApproval
                {
                    StepOrder = step.Order, StepName = step.Name, ApproverUserId = approver, ApproverRole = role,
                    Decision = skip ? ApprovalDecision.Skipped : ApprovalDecision.Pending,
                    Comment = skip ? (approver == requesterId ? "Onaylayıcı talep sahibinin kendisi; adım atlandı." : "Bu adım için onaylayıcı bulunamadı; adım atlandı.") : null,
                });
            }
        }
        await db.SaveChangesAsync(ct);
        await AdvanceAsync(instance, lastApproverId: null, ct);
        audit.Add("workflow.started", "workflow_instance", instance.Id, new { key, subjectType, subjectId });
        await db.SaveChangesAsync(ct);
        return instance;
    }

    /// <summary>Sıradaki karar bekleyen adıma ilerler; kalmadıysa akışı onaylar.</summary>
    private async Task AdvanceAsync(WorkflowInstance instance, int? lastApproverId, CancellationToken ct)
    {
        var steps = instance.Approvals.OrderBy(a => a.StepOrder).ToList();
        foreach (var step in steps.Where(s => s.StepOrder >= instance.CurrentStep))
        {
            instance.CurrentStep = step.StepOrder;
            if (step.Decision == ApprovalDecision.Skipped) continue;
            if (step.Decision == ApprovalDecision.Pending && lastApproverId is not null && step.ApproverUserId == lastApproverId)
            {
                step.Decision = ApprovalDecision.Skipped;
                step.Comment = "Önceki adımı aynı kişi onayladı; adım atlandı.";
                continue;
            }
            if (step.Decision != ApprovalDecision.Pending) continue;

            // Karar bekleyen adım bulundu: onaylayıcıları bilgilendir
            var targets = step.ApproverUserId is { } u ? [u]
                : await db.Users.Where(x => x.IsActive && x.Id != instance.RequesterId && x.Roles.Any(r => r.Role.Name == step.ApproverRole))
                    .Select(x => x.Id).Take(50).ToListAsync(ct);
            foreach (var t in targets) Notify(t, $"Onayınız bekleniyor: {instance.Title}", $"{step.StepName} adımı sizin kararınızı bekliyor.", "/approvals");
            return;
        }
        await CompleteAsync(instance, WorkflowStatus.Approved, null, ct);
    }

    private async Task CompleteAsync(WorkflowInstance instance, WorkflowStatus status, string? comment, CancellationToken ct)
    {
        instance.Status = status;
        instance.CompletedAt = DateTime.UtcNow;
        var handler = Handler(instance.SubjectType);
        if (handler is not null) await handler.OnCompletedAsync(instance, status, comment, ct);
    }

    public async Task CancelAsync(string subjectType, int subjectId, CancellationToken ct = default)
    {
        var instance = await db.WorkflowInstances.Include(i => i.Approvals)
            .FirstOrDefaultAsync(i => i.SubjectType == subjectType && i.SubjectId == subjectId && i.Status == WorkflowStatus.Pending, ct);
        if (instance is null) return;
        foreach (var a in instance.Approvals.Where(a => a.Decision == ApprovalDecision.Pending)) { a.Decision = ApprovalDecision.Skipped; a.Comment = "Talep geri çekildi."; }
        await CompleteAsync(instance, WorkflowStatus.Cancelled, null, ct);
        audit.Add("workflow.cancelled", "workflow_instance", instance.Id);
    }

    // ---------- Karar ----------

    private bool CanDecide(WorkflowInstance i, WorkflowApproval a, int me) =>
        i.Status == WorkflowStatus.Pending && a.StepOrder == i.CurrentStep && a.Decision == ApprovalDecision.Pending && i.RequesterId != me
        && (a.ApproverUserId == me || (a.ApproverUserId is null && a.ApproverRole is not null && currentUser.Roles.Contains(a.ApproverRole)));

    public async Task<WorkflowInstanceDto> DecideAsync(int approvalId, bool approve, string? comment, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var approval = await db.WorkflowApprovals.FirstOrDefaultAsync(a => a.Id == approvalId, ct) ?? throw NotFoundException.For("Onay adımı", approvalId);
        var instance = await db.WorkflowInstances.Include(i => i.Approvals).Include(i => i.Definition).FirstAsync(i => i.Id == approval.WorkflowInstanceId, ct);
        if (instance.Status != WorkflowStatus.Pending) throw new DomainException("Bu talep sonuçlanmış; karar verilemez.");
        approval = instance.Approvals.First(a => a.Id == approvalId);
        if (!CanDecide(instance, approval, me))
            throw new ForbiddenException(instance.RequesterId == me ? "Kendi talebinizi onaylayamazsınız." : "Bu onay adımı size ait değil veya sırası gelmedi.");
        var note = Text.CleanOrNull(comment);
        if (!approve && note is null) throw new DomainException("Reddetme gerekçesi yazın; talep sahibi bu açıklamayı görür.");

        approval.Decision = approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
        approval.DecidedById = me;
        approval.DecidedAt = DateTime.UtcNow;
        approval.Comment = note;
        audit.Add(approve ? "workflow.step_approved" : "workflow.step_rejected", "workflow_instance", instance.Id, new { approval.StepOrder, approval.StepName });

        if (approve)
        {
            instance.CurrentStep = approval.StepOrder + 1;
            await AdvanceAsync(instance, me, ct);
        }
        else
        {
            foreach (var a in instance.Approvals.Where(a => a.Decision == ApprovalDecision.Pending)) { a.Decision = ApprovalDecision.Skipped; a.Comment = "Önceki adımda reddedildi."; }
            await CompleteAsync(instance, WorkflowStatus.Rejected, note, ct);
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(instance.Id, ct);
    }

    // ---------- Onay kutusu ve görüntüleme ----------

    private IQueryable<WorkflowApproval> MyPending(int me)
    {
        var roles = currentUser.Roles.ToList();
        return db.WorkflowApprovals.AsNoTracking().Where(a =>
            a.Decision == ApprovalDecision.Pending && a.Instance.Status == WorkflowStatus.Pending && a.Instance.CurrentStep == a.StepOrder
            && a.Instance.RequesterId != me
            && (a.ApproverUserId == me || (a.ApproverUserId == null && a.ApproverRole != null && roles.Contains(a.ApproverRole))));
    }

    public Task<int> InboxCountAsync(CancellationToken ct = default) => MyPending(currentUser.RequireUserId()).CountAsync(ct);

    public async Task<PagedResult<ApprovalInboxItemDto>> InboxAsync(PagedQuery query, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var page = await MyPending(me).OrderBy(a => a.Instance.CreatedAt)
            .Select(a => new ApprovalInboxItemDto(a.Id, a.WorkflowInstanceId, a.Instance.Definition.Name, a.Instance.Title, null, null, a.StepName,
                a.Instance.RequesterId, a.Instance.Requester.FullName, a.Instance.Requester.Department == null ? null : a.Instance.Requester.Department.Name, a.Instance.CreatedAt))
            .ToPagedResultAsync(query, ct);
        // Konuya özgü özet ve bağlantı
        var subjects = await db.WorkflowInstances.AsNoTracking().Where(i => page.Items.Select(x => x.InstanceId).Contains(i.Id))
            .Select(i => new { i.Id, i.SubjectType, i.SubjectId }).ToListAsync(ct);
        var summaries = new Dictionary<int, (string? Summary, string? Link)>();
        foreach (var g in subjects.GroupBy(s => s.SubjectType))
        {
            var h = Handler(g.Key);
            if (h is null) continue;
            var d = await h.DescribeAsync(g.Select(x => x.SubjectId).ToList(), ct);
            foreach (var s in g) summaries[s.Id] = (d.GetValueOrDefault(s.SubjectId), h.LinkFor(s.SubjectId));
        }
        return page with
        {
            Items = page.Items.Select(x => summaries.TryGetValue(x.InstanceId, out var s) ? x with { Summary = s.Summary, Link = s.Link } : x).ToList(),
        };
    }

    public async Task<WorkflowInstanceDto> GetAsync(int instanceId, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var i = await db.WorkflowInstances.AsNoTracking().Include(x => x.Definition).Include(x => x.Requester)
            .Include(x => x.Approvals).ThenInclude(a => a.ApproverUser).Include(x => x.Approvals).ThenInclude(a => a.DecidedBy)
            .FirstOrDefaultAsync(x => x.Id == instanceId, ct) ?? throw NotFoundException.For("İş akışı", instanceId);
        var involved = i.RequesterId == me || i.Approvals.Any(a => a.ApproverUserId == me || a.DecidedById == me
                       || (a.ApproverRole != null && currentUser.Roles.Contains(a.ApproverRole)));
        if (!involved && !currentUser.HasAny(Permissions.WorkflowsManage, Permissions.LeaveManage))
            throw NotFoundException.For("İş akışı", instanceId);
        var mine = i.Approvals.FirstOrDefault(a => CanDecide(i, a, me));
        return new WorkflowInstanceDto(i.Id, i.Definition.Key, i.Definition.Name, i.Title, i.SubjectType, i.SubjectId, Handler(i.SubjectType)?.LinkFor(i.SubjectId),
            i.RequesterId, i.Requester.FullName, i.Status, i.CreatedAt, i.CompletedAt,
            i.Approvals.OrderBy(a => a.StepOrder).Select(a => new ApprovalStepDto(a.Id, a.StepOrder, a.StepName, a.ApproverUser?.FullName, a.ApproverRole,
                a.Decision, a.DecidedBy?.FullName, a.DecidedAt, a.Comment, i.Status == WorkflowStatus.Pending && a.StepOrder == i.CurrentStep)).ToList(),
            mine is not null, mine?.Id);
    }

    public async Task<WorkflowInstanceDto?> FindBySubjectAsync(string subjectType, int subjectId, CancellationToken ct = default)
    {
        var id = await db.WorkflowInstances.Where(i => i.SubjectType == subjectType && i.SubjectId == subjectId).OrderByDescending(i => i.Id).Select(i => (int?)i.Id).FirstOrDefaultAsync(ct);
        return id is null ? null : await GetAsync(id.Value, ct);
    }

    // ---------- Tanım yönetimi ----------

    public async Task<IReadOnlyList<RoleOptionDto>> RoleOptionsAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.WorkflowsManage);
        return await db.Roles.AsNoTracking().OrderBy(r => r.DisplayName).Select(r => new RoleOptionDto(r.Name, r.DisplayName)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowDefinitionDto>> DefinitionsAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.WorkflowsManage);
        var defs = await db.WorkflowDefinitions.AsNoTracking().Include(d => d.Steps).OrderBy(d => d.Name).ToListAsync(ct);
        var userIds = defs.SelectMany(d => d.Steps).Where(s => s.UserId != null).Select(s => s.UserId!.Value).Distinct().ToList();
        var deptIds = defs.SelectMany(d => d.Steps).Where(s => s.DepartmentId != null).Select(s => s.DepartmentId!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var depts = await db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        return defs.Select(d => new WorkflowDefinitionDto(d.Id, d.Key, d.Name, d.Description, d.IsActive,
            d.Steps.OrderBy(s => s.Order).Select(s => new WorkflowStepDto(s.Id, s.Order, s.Name, s.ApproverType, s.RoleName, s.UserId,
                s.UserId is { } u ? users.GetValueOrDefault(u) : null, s.DepartmentId, s.DepartmentId is { } x ? depts.GetValueOrDefault(x) : null)).ToList())).ToList();
    }

    public async Task<WorkflowDefinitionDto> SaveDefinitionAsync(int id, SaveWorkflowRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.WorkflowsManage);
        var def = await db.WorkflowDefinitions.Include(d => d.Steps).FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw NotFoundException.For("İş akışı", id);
        foreach (var s in r.Steps)
        {
            if (s.ApproverType == ApproverType.Role && !await db.Roles.AnyAsync(x => x.Name == s.RoleName, ct)) throw new DomainException($"\"{s.Name}\" adımı için geçerli bir rol seçin.");
            if (s.ApproverType == ApproverType.User && !await db.Users.AnyAsync(x => x.Id == s.UserId && x.IsActive, ct)) throw new DomainException($"\"{s.Name}\" adımı için aktif bir kişi seçin.");
            if (s.DepartmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d && x.IsActive, ct)) throw NotFoundException.For("Departman", d);
        }
        def.Name = Text.Clean(r.Name);
        def.Description = Text.CleanOrNull(r.Description);
        def.IsActive = r.IsActive;
        def.UpdatedAt = DateTime.UtcNow;
        // Adımlar yeniden yazılır; başlamış akışlar etkilenmez (onaylayıcılar başlarken kopyalandı).
        db.WorkflowStepDefinitions.RemoveRange(def.Steps);
        var order = 1;
        foreach (var s in r.Steps)
            db.WorkflowStepDefinitions.Add(new WorkflowStepDefinition
            {
                WorkflowDefinitionId = def.Id, Order = order++, Name = Text.Clean(s.Name), ApproverType = s.ApproverType,
                RoleName = s.ApproverType == ApproverType.Role ? s.RoleName : null,
                UserId = s.ApproverType == ApproverType.User ? s.UserId : null,
                DepartmentId = s.ApproverType == ApproverType.DepartmentManager ? s.DepartmentId : null,
            });
        audit.Add("workflow.definition_updated", "workflow_definition", def.Id, new { def.Key, steps = r.Steps.Count, def.IsActive });
        await db.SaveChangesAsync(ct);
        return (await DefinitionsAsync(ct)).First(x => x.Id == id);
    }
}

public sealed class SaveWorkflowRequestValidator : AbstractValidator<SaveWorkflowRequest>
{
    public SaveWorkflowRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Akış adı zorunludur.").MaximumLength(100).NoMarkup();
        RuleFor(x => x.Description).MaximumLength(500).NoMarkup();
        RuleFor(x => x.Steps).NotNull().Must(s => s.Count <= 10).WithMessage("En fazla 10 adım tanımlanabilir.");
        RuleForEach(x => x.Steps).ChildRules(s =>
        {
            s.RuleFor(x => x.Name).NotEmpty().WithMessage("Adım adı zorunludur.").MaximumLength(100).NoMarkup();
            s.RuleFor(x => x.ApproverType).IsInEnum();
            s.RuleFor(x => x.RoleName).NotEmpty().When(x => x.ApproverType == ApproverType.Role).WithMessage("Rol seçin.");
            s.RuleFor(x => x.UserId).NotNull().When(x => x.ApproverType == ApproverType.User).WithMessage("Kişi seçin.");
        });
    }
}

public sealed class DecideRequestValidator : AbstractValidator<DecideRequest>
{
    public DecideRequestValidator() => RuleFor(x => x.Comment).MaximumLength(1000).NoMarkup();
}
