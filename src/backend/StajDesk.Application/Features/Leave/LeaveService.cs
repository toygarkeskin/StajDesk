using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Workflows;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Leave;

/* =========================================================
   İzin sistemi. İzin talebi "leave" iş akışıyla onaylanır (varsayılan: yönetici → İzin yöneticisi).
   Bakiye: hak (kişiye özel tanım yoksa türün varsayılanı) − onaylanan − bekleyen.
   İzin yönetimi (leave.manage) İK rolünden AYRI bir izindir.
   ========================================================= */

public sealed record LeaveTypeDto(int Id, string Code, string Name, string? Description, bool TracksBalance, decimal DefaultDaysPerYear, bool IsPaid, bool IsActive, int SortOrder);
public sealed record SaveLeaveTypeRequest(string Code, string Name, string? Description, bool TracksBalance, decimal DefaultDaysPerYear, bool IsPaid, bool IsActive, int SortOrder);
public sealed record LeaveBalanceDto(int LeaveTypeId, string LeaveType, int Year, decimal Entitled, bool IsCustom, decimal Used, decimal Pending, decimal Remaining);
public sealed record SetEntitlementRequest(int UserId, int LeaveTypeId, int Year, decimal Days, string? Note);
public sealed record CreateLeaveRequest(int LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string? Reason);
public sealed record LeaveRequestDto(int Id, int UserId, string UserName, string? Department, int LeaveTypeId, string LeaveType, DateOnly StartDate, DateOnly EndDate,
    decimal Days, string? Reason, LeaveStatus Status, DateTime CreatedAt, int? WorkflowInstanceId);
public sealed record LeaveRequestQuery : PagedQuery
{
    public LeaveStatus? Status { get; init; }
    public int? UserId { get; init; }
    public bool All { get; init; }
}
public sealed record LeaveDaysDto(decimal Days, IReadOnlyList<DateOnly> ExcludedHolidays);

public interface ILeaveService
{
    Task<IReadOnlyList<LeaveTypeDto>> TypesAsync(bool includeInactive, CancellationToken ct = default);
    Task<LeaveTypeDto> SaveTypeAsync(int? id, SaveLeaveTypeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LeaveBalanceDto>> BalancesAsync(int? userId, int? year, CancellationToken ct = default);
    Task SetEntitlementAsync(SetEntitlementRequest request, CancellationToken ct = default);
    Task<LeaveDaysDto> CountDaysAsync(DateOnly start, DateOnly end, CancellationToken ct = default);
    Task<LeaveRequestDto> CreateAsync(CreateLeaveRequest request, CancellationToken ct = default);
    Task<PagedResult<LeaveRequestDto>> ListAsync(LeaveRequestQuery query, CancellationToken ct = default);
    Task<LeaveRequestDto> GetAsync(int id, CancellationToken ct = default);
    Task CancelAsync(int id, CancellationToken ct = default);
}

public static class LeaveSubjects
{
    public const string Request = "leave_request";
}

/// <summary>İş günü hesabı: hafta sonu ve sabit tarihli resmî tatiller hariç. (Dinî bayramlar her yıl değiştiği için tabloya eklenmeli — docs/IZIN.md)</summary>
public static class WorkingDays
{
    private static readonly (int Month, int Day)[] FixedHolidays = [(1, 1), (4, 23), (5, 1), (5, 19), (7, 15), (8, 30), (10, 29)];
    public static bool IsHoliday(DateOnly d) => FixedHolidays.Contains((d.Month, d.Day));
    public static (decimal Days, List<DateOnly> Holidays) Count(DateOnly start, DateOnly end)
    {
        decimal days = 0;
        var holidays = new List<DateOnly>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (IsHoliday(d)) { holidays.Add(d); continue; }
            days++;
        }
        return (days, holidays);
    }
}

internal sealed class LeaveService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, IWorkflowEngine workflow, TimeProvider clock) : ILeaveService
{
    public const string SubjectType = LeaveSubjects.Request;
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));
    private bool CanManage => currentUser.Has(Permissions.LeaveManage);

    // ---------- Türler ----------

    private static LeaveTypeDto ToDto(LeaveType t) => new(t.Id, t.Code, t.Name, t.Description, t.TracksBalance, t.DefaultDaysPerYear, t.IsPaid, t.IsActive, t.SortOrder);

    public async Task<IReadOnlyList<LeaveTypeDto>> TypesAsync(bool includeInactive, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var q = db.LeaveTypes.AsNoTracking();
        if (!(includeInactive && CanManage)) q = q.Where(t => t.IsActive);
        return (await q.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<LeaveTypeDto> SaveTypeAsync(int? id, SaveLeaveTypeRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.LeaveManage);
        var code = r.Code.Trim().ToUpperInvariant();
        if (await db.LeaveTypes.AnyAsync(t => t.Code == code && t.Id != id, ct)) throw new ConflictException($"{code} kodlu bir izin türü zaten var.");
        var t = id is null ? db.LeaveTypes.Add(new LeaveType()).Entity
            : await db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("İzin türü", id);
        t.Code = code;
        t.Name = Text.Clean(r.Name);
        t.Description = Text.CleanOrNull(r.Description);
        t.TracksBalance = r.TracksBalance;
        t.DefaultDaysPerYear = r.TracksBalance ? r.DefaultDaysPerYear : 0;
        t.IsPaid = r.IsPaid;
        t.IsActive = r.IsActive;
        t.SortOrder = r.SortOrder;
        if (id is not null) t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        audit.Add(id is null ? "leave_type.created" : "leave_type.updated", "leave_type", t.Id, new { t.Code, t.TracksBalance, t.DefaultDaysPerYear });
        await db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    // ---------- Bakiye ----------

    public async Task<IReadOnlyList<LeaveBalanceDto>> BalancesAsync(int? userId, int? year, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var uid = userId ?? me;
        if (uid != me && !CanManage) throw new ForbiddenException("Başka bir çalışanın izin bakiyesini yalnızca izin yöneticisi görebilir.");
        var y = year ?? Today.Year;
        var types = await db.LeaveTypes.AsNoTracking().Where(t => t.IsActive && t.TracksBalance).OrderBy(t => t.SortOrder).ToListAsync(ct);
        var custom = await db.LeaveEntitlements.AsNoTracking().Where(e => e.UserId == uid && e.Year == y).ToDictionaryAsync(e => e.LeaveTypeId, ct);
        var requests = await db.LeaveRequests.AsNoTracking()
            .Where(r => r.UserId == uid && r.StartDate.Year == y && (r.Status == LeaveStatus.Approved || r.Status == LeaveStatus.Pending))
            .GroupBy(r => new { r.LeaveTypeId, r.Status }).Select(g => new { g.Key.LeaveTypeId, g.Key.Status, Days = g.Sum(x => x.Days) }).ToListAsync(ct);
        return types.Select(t =>
        {
            var entitled = custom.TryGetValue(t.Id, out var c) ? c.Days : t.DefaultDaysPerYear;
            var used = requests.Where(x => x.LeaveTypeId == t.Id && x.Status == LeaveStatus.Approved).Sum(x => x.Days);
            var pending = requests.Where(x => x.LeaveTypeId == t.Id && x.Status == LeaveStatus.Pending).Sum(x => x.Days);
            return new LeaveBalanceDto(t.Id, t.Name, y, entitled, c is not null, used, pending, entitled - used - pending);
        }).ToList();
    }

    public async Task SetEntitlementAsync(SetEntitlementRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.LeaveManage);
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId, ct)) throw NotFoundException.For("Çalışan", r.UserId);
        if (!await db.LeaveTypes.AnyAsync(t => t.Id == r.LeaveTypeId && t.TracksBalance, ct)) throw new DomainException("Bu izin türü için bakiye tutulmuyor.");
        var e = await db.LeaveEntitlements.FirstOrDefaultAsync(x => x.UserId == r.UserId && x.LeaveTypeId == r.LeaveTypeId && x.Year == r.Year, ct);
        if (e is null) db.LeaveEntitlements.Add(e = new LeaveEntitlement { UserId = r.UserId, LeaveTypeId = r.LeaveTypeId, Year = r.Year });
        e.Days = r.Days;
        e.Note = Text.CleanOrNull(r.Note);
        e.UpdatedAt = DateTime.UtcNow;
        audit.Add("leave.entitlement_set", "user", r.UserId, new { r.LeaveTypeId, r.Year, r.Days });
        await db.SaveChangesAsync(ct);
    }

    // ---------- Talepler ----------

    public Task<LeaveDaysDto> CountDaysAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        if (end < start || end.DayNumber - start.DayNumber > 120) return Task.FromResult(new LeaveDaysDto(0, []));
        var (days, holidays) = WorkingDays.Count(start, end);
        return Task.FromResult(new LeaveDaysDto(days, holidays));
    }

    public async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.LeaveRequest);
        var me = currentUser.RequireUserId();
        var type = await db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == r.LeaveTypeId && t.IsActive, ct) ?? throw NotFoundException.For("İzin türü", r.LeaveTypeId);
        if (r.StartDate < Today.AddDays(-30)) throw new DomainException("30 günden eski tarihler için izin talebi açılamaz; izin yöneticisiyle görüşün.");
        if (r.StartDate.Year != r.EndDate.Year) throw new DomainException("Yıl dönümünü kapsayan izni iki ayrı talep olarak açın (bakiyeler yıllıktır).");
        var (days, _) = WorkingDays.Count(r.StartDate, r.EndDate);
        if (days <= 0) throw new DomainException("Seçilen aralıkta iş günü yok (hafta sonu veya resmî tatil).");
        if (await db.LeaveRequests.AnyAsync(x => x.UserId == me && (x.Status == LeaveStatus.Pending || x.Status == LeaveStatus.Approved)
                                                 && x.StartDate <= r.EndDate && x.EndDate >= r.StartDate, ct))
            throw new ConflictException("Bu tarihlerle çakışan bekleyen veya onaylanmış bir izniniz var.");
        if (type.TracksBalance)
        {
            var balance = (await BalancesAsync(null, r.StartDate.Year, ct)).FirstOrDefault(b => b.LeaveTypeId == type.Id);
            if (balance is null || balance.Remaining < days)
                throw new DomainException($"{type.Name} bakiyeniz yetersiz: {r.StartDate.Year} için kalan {balance?.Remaining ?? 0:0.#} gün, talep {days:0.#} gün.");
        }

        var request = new LeaveRequest { UserId = me, LeaveTypeId = type.Id, StartDate = r.StartDate, EndDate = r.EndDate, Days = days, Reason = Text.CleanOrNull(r.Reason) };
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync(ct);
        audit.Add("leave.requested", "leave_request", request.Id, new { type.Code, r.StartDate, r.EndDate, days });
        var title = $"{currentUser.FullName} · {type.Name} · {days:0.#} gün ({r.StartDate:dd.MM}–{r.EndDate:dd.MM.yyyy})";
        var instance = await workflow.StartAsync(WorkflowKeys.Leave, SubjectType, request.Id, title, me, ct);
        request.WorkflowInstanceId = instance.Id;
        await db.SaveChangesAsync(ct);
        return await GetAsync(request.Id, ct);
    }

    private static readonly System.Linq.Expressions.Expression<Func<LeaveRequest, LeaveRequestDto>> Projection = r => new LeaveRequestDto(
        r.Id, r.UserId, r.User.FullName, r.User.Department == null ? null : r.User.Department.Name, r.LeaveTypeId, r.LeaveType.Name,
        r.StartDate, r.EndDate, r.Days, r.Reason, r.Status, r.CreatedAt, r.WorkflowInstanceId);

    public async Task<PagedResult<LeaveRequestDto>> ListAsync(LeaveRequestQuery query, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var q = db.LeaveRequests.AsNoTracking();
        if (query.All && CanManage) { if (query.UserId is not null) q = q.Where(r => r.UserId == query.UserId); }
        else q = q.Where(r => r.UserId == me);
        if (query.Status is not null) q = q.Where(r => r.Status == query.Status);
        return await q.OrderByDescending(r => r.StartDate).ThenByDescending(r => r.Id).Select(Projection).ToPagedResultAsync(query, ct);
    }

    public async Task<LeaveRequestDto> GetAsync(int id, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var dto = await db.LeaveRequests.AsNoTracking().Where(r => r.Id == id).Select(Projection).FirstOrDefaultAsync(ct) ?? throw NotFoundException.For("İzin talebi", id);
        if (dto.UserId == me || CanManage) return dto;
        // Onay zincirindeki kişi de görebilir
        var roles = currentUser.Roles.ToList();
        var approver = dto.WorkflowInstanceId is { } wi && await db.WorkflowApprovals.AnyAsync(a => a.WorkflowInstanceId == wi
            && (a.ApproverUserId == me || a.DecidedById == me || (a.ApproverRole != null && roles.Contains(a.ApproverRole))), ct);
        return approver ? dto : throw NotFoundException.For("İzin talebi", id);
    }

    public async Task CancelAsync(int id, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var r = await db.LeaveRequests.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("İzin talebi", id);
        if (r.UserId != me && !CanManage) throw NotFoundException.For("İzin talebi", id);
        if (r.Status == LeaveStatus.Approved && r.StartDate <= Today) throw new DomainException("Başlamış onaylı izin geri çekilemez; izin yöneticisiyle görüşün.");
        if (r.Status is not (LeaveStatus.Pending or LeaveStatus.Approved)) throw new DomainException("Bu talep zaten sonuçlanmış.");
        if (r.Status == LeaveStatus.Pending) await workflow.CancelAsync(SubjectType, r.Id, ct);
        r.Status = LeaveStatus.Cancelled;
        r.UpdatedAt = DateTime.UtcNow;
        audit.Add("leave.cancelled", "leave_request", id);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>"leave" iş akışı sonucunu izin talebine uygular ve çalışanı bilgilendirir.</summary>
internal sealed class LeaveWorkflowHandler(IAppDbContext db) : IWorkflowSubjectHandler
{
    public string SubjectType => LeaveService.SubjectType;
    public string LinkFor(int subjectId) => $"/leave?request={subjectId}";

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowStatus status, string? comment, CancellationToken ct)
    {
        var r = await db.LeaveRequests.Include(x => x.LeaveType).FirstOrDefaultAsync(x => x.Id == instance.SubjectId, ct);
        if (r is null || r.Status != LeaveStatus.Pending) return;
        r.Status = status switch { WorkflowStatus.Approved => LeaveStatus.Approved, WorkflowStatus.Rejected => LeaveStatus.Rejected, _ => LeaveStatus.Cancelled };
        r.UpdatedAt = DateTime.UtcNow;
        if (status is WorkflowStatus.Approved or WorkflowStatus.Rejected)
            db.Notifications.Add(new Notification
            {
                UserId = r.UserId, Link = LinkFor(r.Id),
                Title = status == WorkflowStatus.Approved ? $"İzniniz onaylandı: {r.LeaveType.Name}" : $"İzin talebiniz reddedildi: {r.LeaveType.Name}",
                Message = status == WorkflowStatus.Approved
                    ? $"{r.StartDate:dd.MM.yyyy} – {r.EndDate:dd.MM.yyyy} ({r.Days:0.#} gün) onaylandı."
                    : $"Gerekçe: {comment ?? "belirtilmedi"}",
            });
    }

    public async Task<IReadOnlyDictionary<int, string>> DescribeAsync(IReadOnlyCollection<int> subjectIds, CancellationToken ct) =>
        (await db.LeaveRequests.AsNoTracking().Where(r => subjectIds.Contains(r.Id))
            .Select(r => new { r.Id, Type = r.LeaveType.Name, r.Days, r.StartDate, r.EndDate, r.Reason }).ToListAsync(ct))
        .ToDictionary(r => r.Id, r => $"{r.Type} · {r.Days:0.#} iş günü · {r.StartDate:dd.MM.yyyy} – {r.EndDate:dd.MM.yyyy}{(r.Reason is null ? "" : " · " + r.Reason)}");
}

public sealed class CreateLeaveRequestValidator : AbstractValidator<CreateLeaveRequest>
{
    public CreateLeaveRequestValidator()
    {
        RuleFor(x => x.LeaveTypeId).GreaterThan(0).WithMessage("İzin türünü seçin.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
        RuleFor(x => x).Must(x => x.EndDate.DayNumber - x.StartDate.DayNumber <= 120).WithMessage("Tek talepte en fazla 120 günlük aralık seçilebilir.");
        RuleFor(x => x.Reason).MaximumLength(500).NoMarkup();
    }
}

public sealed class SaveLeaveTypeRequestValidator : AbstractValidator<SaveLeaveTypeRequest>
{
    public SaveLeaveTypeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches("^[A-Za-z0-9_-]{2,20}$").WithMessage("Kod 2–20 karakter; harf, rakam, tire.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Ad zorunludur.").MaximumLength(100).NoMarkup();
        RuleFor(x => x.Description).MaximumLength(500).NoMarkup();
        RuleFor(x => x.DefaultDaysPerYear).InclusiveBetween(0, 365);
    }
}

public sealed class SetEntitlementRequestValidator : AbstractValidator<SetEntitlementRequest>
{
    public SetEntitlementRequestValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(0, 365).WithMessage("Hak 0–365 gün arasında olmalıdır.");
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Note).MaximumLength(300).NoMarkup();
    }
}
