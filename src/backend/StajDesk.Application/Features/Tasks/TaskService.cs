using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Tasks;

/* =========================================================
   Görev yönetimi.
   - Herkes kendine görev açabilir.
   - Başkasına atama: atanan kişi organizasyonda sizin altınızdaysa (doğrudan veya dolaylı rapor) ya da tasks.manage izniniz varsa.
   - Görevi atanan, açan ve tasks.manage sahibi görür. Durumu atanan veya açan değiştirir; içeriği açan (veya tasks.manage) düzenler.
   ========================================================= */

public sealed record TaskPersonDto(int Id, string FullName);
public sealed record WorkTaskDto(int Id, string Title, string? Description, TaskPersonDto Assignee, TaskPersonDto CreatedBy, DateOnly? DueDate,
    WorkTaskPriority Priority, WorkTaskStatus Status, DateTime CreatedAt, DateTime? CompletedAt, bool IsOverdue, bool CanEdit, bool CanChangeStatus);
public sealed record SaveTaskRequest(string Title, string? Description, int? AssigneeId, DateOnly? DueDate, WorkTaskPriority Priority);
public sealed record ChangeTaskStatusRequest(WorkTaskStatus Status);
public sealed record TaskQuery : PagedQuery
{
    /// <summary>mine (bana atanan) | created (verdiğim) | all (tasks.manage)</summary>
    public string? Scope { get; init; }
    public WorkTaskStatus? Status { get; init; }
    public bool? OpenOnly { get; init; }
}
public sealed record TaskSummaryDto(int Open, int Overdue, int DueToday);

public interface ITaskService
{
    Task<PagedResult<WorkTaskDto>> ListAsync(TaskQuery query, CancellationToken ct = default);
    Task<TaskSummaryDto> SummaryAsync(CancellationToken ct = default);
    Task<WorkTaskDto> GetAsync(int id, CancellationToken ct = default);
    Task<WorkTaskDto> SaveAsync(int? id, SaveTaskRequest request, CancellationToken ct = default);
    Task<WorkTaskDto> ChangeStatusAsync(int id, WorkTaskStatus status, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

internal sealed class TaskService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, TimeProvider clock) : ITaskService
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));
    private bool CanManage => currentUser.Has(Permissions.TasksManage);

    private IQueryable<WorkTask> Visible(int me) => CanManage ? db.WorkTasks.AsNoTracking()
        : db.WorkTasks.AsNoTracking().Where(t => t.AssigneeId == me || t.CreatedById == me);

    private WorkTaskDto ToDto(WorkTask t, int me) => new(t.Id, t.Title, t.Description,
        new TaskPersonDto(t.Assignee.Id, t.Assignee.FullName), new TaskPersonDto(t.CreatedBy.Id, t.CreatedBy.FullName),
        t.DueDate, t.Priority, t.Status, t.CreatedAt, t.CompletedAt,
        t.DueDate is { } d && d < Today && t.Status is WorkTaskStatus.Todo or WorkTaskStatus.InProgress,
        t.CreatedById == me || CanManage, t.AssigneeId == me || t.CreatedById == me || CanManage);

    public async Task<PagedResult<WorkTaskDto>> ListAsync(TaskQuery query, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var q = Visible(me).Include(t => t.Assignee).Include(t => t.CreatedBy).AsQueryable();
        q = query.Scope switch
        {
            "created" => q.Where(t => t.CreatedById == me),
            "all" when CanManage => q,
            _ => q.Where(t => t.AssigneeId == me),
        };
        if (query.Status is not null) q = q.Where(t => t.Status == query.Status);
        if (query.OpenOnly == true) q = q.Where(t => t.Status == WorkTaskStatus.Todo || t.Status == WorkTaskStatus.InProgress);
        // Açık görevler önce, sonra teslim tarihine göre (tarihsizler sonda)
        var page = await q.OrderBy(t => t.Status == WorkTaskStatus.Done || t.Status == WorkTaskStatus.Cancelled)
            .ThenBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority).ThenByDescending(t => t.Id)
            .ToPagedResultAsync(query, ct);
        return new PagedResult<WorkTaskDto>(page.Items.Select(t => ToDto(t, me)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<TaskSummaryDto> SummaryAsync(CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var today = Today;
        var open = db.WorkTasks.AsNoTracking().Where(t => t.AssigneeId == me && (t.Status == WorkTaskStatus.Todo || t.Status == WorkTaskStatus.InProgress));
        return new TaskSummaryDto(await open.CountAsync(ct), await open.CountAsync(t => t.DueDate < today, ct), await open.CountAsync(t => t.DueDate == today, ct));
    }

    private async Task<WorkTask> LoadAsync(int id, int me, CancellationToken ct) =>
        await db.WorkTasks.Include(t => t.Assignee).Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id, ct) is { } t
        && (t.AssigneeId == me || t.CreatedById == me || CanManage) ? t : throw NotFoundException.For("Görev", id);

    public async Task<WorkTaskDto> GetAsync(int id, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        return ToDto(await LoadAsync(id, me, ct), me);
    }

    /// <summary>Atanan kişi, atayanın organizasyondaki ekibinde mi? (yönetici zinciri yukarı doğru en fazla 15 basamak)</summary>
    private async Task<bool> IsInMyTeamAsync(int me, int assigneeId, CancellationToken ct)
    {
        int? current = assigneeId;
        for (var i = 0; i < 15 && current is not null; i++)
        {
            current = await db.Users.Where(u => u.Id == current).Select(u => u.ManagerId).FirstOrDefaultAsync(ct);
            if (current == me) return true;
        }
        return false;
    }

    public async Task<WorkTaskDto> SaveAsync(int? id, SaveTaskRequest r, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var assigneeId = r.AssigneeId ?? me;
        WorkTask t;
        if (id is null) t = db.WorkTasks.Add(new WorkTask { CreatedById = me }).Entity;
        else
        {
            t = await LoadAsync(id.Value, me, ct);
            if (t.CreatedById != me && !CanManage) throw new ForbiddenException("Görevi yalnızca açan kişi düzenleyebilir; siz yalnızca durumunu değiştirebilirsiniz.");
            t.UpdatedAt = DateTime.UtcNow;
        }
        var reassigned = t.AssigneeId != assigneeId;
        if (reassigned && assigneeId != me)
        {
            if (!await db.Users.AnyAsync(u => u.Id == assigneeId && u.IsActive, ct)) throw NotFoundException.For("Çalışan", assigneeId);
            if (!CanManage && !await IsInMyTeamAsync(me, assigneeId, ct))
                throw new ForbiddenException("Görevi yalnızca kendinize veya organizasyonda size bağlı çalışanlara atayabilirsiniz.");
        }
        t.Title = Text.Clean(r.Title);
        t.Description = Text.CleanOrNull(r.Description);
        t.AssigneeId = assigneeId;
        t.DueDate = r.DueDate;
        t.Priority = r.Priority;
        await db.SaveChangesAsync(ct);
        if (reassigned && assigneeId != me)
            db.Notifications.Add(new Notification { UserId = assigneeId, Title = $"Size görev atandı: {t.Title}", Link = $"/tasks?task={t.Id}",
                Message = $"{currentUser.FullName} size bir görev verdi{(t.DueDate is { } d ? $"; teslim tarihi {d:dd.MM.yyyy}" : "")}." });
        audit.Add(id is null ? "task.created" : "task.updated", "task", t.Id, new { t.AssigneeId, t.DueDate });
        await db.SaveChangesAsync(ct);
        return await GetAsync(t.Id, ct);
    }

    public async Task<WorkTaskDto> ChangeStatusAsync(int id, WorkTaskStatus status, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var t = await LoadAsync(id, me, ct);
        if (t.Status == status) return ToDto(t, me);
        t.Status = status;
        t.CompletedAt = status == WorkTaskStatus.Done ? DateTime.UtcNow : null;
        t.UpdatedAt = DateTime.UtcNow;
        if (status == WorkTaskStatus.Done && t.CreatedById != me)
            db.Notifications.Add(new Notification { UserId = t.CreatedById, Title = $"Görev tamamlandı: {t.Title}", Link = $"/tasks?task={t.Id}", Message = $"{currentUser.FullName} görevi tamamladı." });
        audit.Add("task.status_changed", "task", id, new { status = status.ToString() });
        await db.SaveChangesAsync(ct);
        return ToDto(t, me);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var t = await LoadAsync(id, me, ct);
        if (t.CreatedById != me && !CanManage) throw new ForbiddenException("Görevi yalnızca açan kişi silebilir.");
        db.WorkTasks.Remove(t);
        audit.Add("task.deleted", "task", id, new { t.Title });
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SaveTaskRequestValidator : AbstractValidator<SaveTaskRequest>
{
    public SaveTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Görev başlığı zorunludur.").MaximumLength(200).NoMarkup();
        RuleFor(x => x.Description).MaximumLength(4000).NoMarkup();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class ChangeTaskStatusRequestValidator : AbstractValidator<ChangeTaskStatusRequest>
{
    public ChangeTaskStatusRequestValidator() => RuleFor(x => x.Status).IsInEnum();
}
