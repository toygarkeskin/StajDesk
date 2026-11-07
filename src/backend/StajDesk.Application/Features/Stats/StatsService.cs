using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Features.Tickets;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Enums;

namespace StajDesk.Application.Features.Stats;

public sealed record CountItem(string Key, string Label, int Count, string? Color = null);
public sealed record DailyPoint(DateOnly Date, int Created, int Resolved);

public sealed record DashboardStatsDto(
    int Total,
    int Open,
    int InProgress,
    int Resolved,
    int Closed,
    int Unassigned,
    int CriticalOpen,
    double? AvgResolutionHours,
    IReadOnlyList<CountItem> ByStatus,
    IReadOnlyList<CountItem> ByPriority,
    IReadOnlyList<CountItem> ByCategory,
    IReadOnlyList<DailyPoint> Last14Days,
    IReadOnlyList<CountItem> TopAgents,
    IReadOnlyList<TicketListItemDto> Recent,
    int AssetCount,
    DateTime GeneratedAt);

public interface IStatsService
{
    Task<DashboardStatsDto> GetDashboardAsync(CancellationToken ct = default);
}

internal sealed class StatsService(IAppDbContext db, ICurrentUser currentUser, ICacheService cache) : IStatsService
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public Task<DashboardStatsDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        int? scopeUser = currentUser.IsStaff ? null : userId;
        return cache.GetOrSetAsync(CacheKeys.Stats(scopeUser), token => BuildAsync(scopeUser, token), CacheTtl, ct);
    }

    private async Task<DashboardStatsDto> BuildAsync(int? scopeUser, CancellationToken ct)
    {
        var tickets = db.Tickets.AsNoTracking();
        if (scopeUser is not null) tickets = tickets.Where(t => t.RequesterId == scopeUser);

        var byStatus = await tickets.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int S(TicketStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;

        var byPriority = await tickets.Where(t => t.Status != TicketStatus.Closed)
            .GroupBy(t => t.Priority).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        var byCategory = await tickets.GroupBy(t => new { t.CategoryId, t.Category.Name, t.Category.Color })
            .Select(g => new { g.Key.Name, g.Key.Color, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(8).ToListAsync(ct);

        var since = DateTime.UtcNow.Date.AddDays(-13);
        var createdDates = await tickets.Where(t => t.CreatedAt >= since).Select(t => t.CreatedAt).ToListAsync(ct);
        var resolvedDates = await tickets.Where(t => t.ResolvedAt != null && t.ResolvedAt >= since)
            .Select(t => t.ResolvedAt!.Value).ToListAsync(ct);
        var days = Enumerable.Range(0, 14).Select(i => DateOnly.FromDateTime(since.AddDays(i)))
            .Select(d => new DailyPoint(d,
                createdDates.Count(x => DateOnly.FromDateTime(x) == d),
                resolvedDates.Count(x => DateOnly.FromDateTime(x) == d)))
            .ToList();

        var resolvedPairs = await tickets.Where(t => t.ResolvedAt != null && t.CreatedAt >= DateTime.UtcNow.AddDays(-90))
            .Select(t => new { t.CreatedAt, ResolvedAt = t.ResolvedAt!.Value }).ToListAsync(ct);
        double? avgHours = resolvedPairs.Count == 0 ? null
            : Math.Round(resolvedPairs.Average(p => (p.ResolvedAt - p.CreatedAt).TotalHours), 1);

        var topAgents = new List<CountItem>();
        if (scopeUser is null)
        {
            var rows = await tickets
                .Where(t => t.AssigneeId != null && t.Status != TicketStatus.Closed)
                .GroupBy(t => new { t.AssigneeId, t.Assignee!.FullName })
                .Select(g => new { g.Key.AssigneeId, g.Key.FullName, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(5).ToListAsync(ct);
            topAgents = rows.Select(a => new CountItem(a.AssigneeId!.Value.ToString(), a.FullName, a.Count)).ToList();
        }
        var isStaffScope = scopeUser is null;

        var recent = await tickets.OrderByDescending(t => t.CreatedAt).Take(6)
            .Select(t => new TicketListItemDto(t.Id, t.Title, t.Status, t.Priority, t.Type,
                new CategoryRefDto(t.Category.Id, t.Category.Name, t.Category.Color),
                new UserSummaryDto(t.Requester.Id, t.Requester.FullName, t.Requester.Email, t.Requester.Role),
                t.Assignee == null ? null : new UserSummaryDto(t.Assignee.Id, t.Assignee.FullName, t.Assignee.Email, t.Assignee.Role),
                t.Comments.Count(c => !c.IsInternal || isStaffScope), t.CreatedAt, t.UpdatedAt))
            .ToListAsync(ct);

        var assetCount = scopeUser is null
            ? await db.Assets.CountAsync(ct)
            : await db.Assets.CountAsync(a => a.AssignedUserId == scopeUser, ct);

        return new DashboardStatsDto(
            Total: byStatus.Sum(x => x.Count),
            Open: S(TicketStatus.Open),
            InProgress: S(TicketStatus.InProgress),
            Resolved: S(TicketStatus.Resolved),
            Closed: S(TicketStatus.Closed),
            Unassigned: await tickets.CountAsync(t => t.AssigneeId == null && t.Status != TicketStatus.Closed, ct),
            CriticalOpen: await tickets.CountAsync(t => t.Priority == TicketPriority.Critical && t.Status != TicketStatus.Closed, ct),
            AvgResolutionHours: avgHours,
            ByStatus: Enum.GetValues<TicketStatus>().Select(s => new CountItem(s.ToString(), StatusLabel(s), S(s))).ToList(),
            ByPriority: Enum.GetValues<TicketPriority>()
                .Select(p => new CountItem(p.ToString(), PriorityLabel(p), byPriority.FirstOrDefault(x => x.Key == p)?.Count ?? 0)).ToList(),
            ByCategory: byCategory.Select(c => new CountItem(c.Name, c.Name, c.Count, c.Color)).ToList(),
            Last14Days: days,
            TopAgents: topAgents,
            Recent: recent,
            AssetCount: assetCount,
            GeneratedAt: DateTime.UtcNow);
    }

    private static string StatusLabel(TicketStatus s) => s switch
    {
        TicketStatus.Open => "Açık",
        TicketStatus.InProgress => "İşlemde",
        TicketStatus.Resolved => "Çözüldü",
        _ => "Kapandı"
    };

    private static string PriorityLabel(TicketPriority p) => p switch
    {
        TicketPriority.Low => "Düşük",
        TicketPriority.Medium => "Orta",
        TicketPriority.High => "Yüksek",
        _ => "Kritik"
    };
}
