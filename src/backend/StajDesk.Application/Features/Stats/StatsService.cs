using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Features.Tickets;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Stats;

public sealed record CountItem(string Key, string Label, int Count, string? Color = null);
public sealed record DailyPoint(DateOnly Date, int Created, int Resolved);

public sealed record DashboardStatsDto(
    string Scope,
    string ScopeLabel,
    int Total, int Open, int InProgress, int Pending, int Resolved, int Closed,
    int Unassigned, int CriticalOpen, double? AvgResolutionHours,
    IReadOnlyList<CountItem> ByStatus,
    IReadOnlyList<CountItem> ByPriority,
    IReadOnlyList<CountItem> ByCategory,
    IReadOnlyList<CountItem> ByStore,
    IReadOnlyList<DailyPoint> Last14Days,
    IReadOnlyList<CountItem> TopAgents,
    IReadOnlyList<TicketListItemDto> Recent,
    int AssetCount, int UserCount, int StoreCount,
    DateTime GeneratedAt);

public interface IStatsService
{
    Task<DashboardStatsDto> GetDashboardAsync(CancellationToken ct = default);
}

/// <summary>
/// Özet paneli üç kapsamda çalışır (role değil izne göre):
///  - reports.read.all   → tüm şirket (yönetici, destek personeli)
///  - reports.read.store → kendi mağazası (mağaza sahibi)
///  - diğerleri          → yalnızca kendi talepleri
/// Sonuç Redis'te 60 sn önbelleklenir; talep değişince "stats:*" anahtarları silinir.
/// </summary>
internal sealed class StatsService(IAppDbContext db, ICurrentUser currentUser, ICacheService cache) : IStatsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public Task<DashboardStatsDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var (scope, key) = currentUser.Has(Permissions.ReportsReadAll) ? ("all", "stats:all")
            : currentUser.Has(Permissions.ReportsReadStore) && currentUser.StoreId is { } sid ? ("store", $"stats:store:{sid}")
            : ("user", $"stats:user:{userId}");
        return cache.GetOrSetAsync(key, token => BuildAsync(scope, userId, currentUser.StoreId, token), CacheTtl, ct);
    }

    private async Task<DashboardStatsDto> BuildAsync(string scope, int userId, int? storeId, CancellationToken ct)
    {
        IQueryable<Ticket> tickets = db.Tickets.AsNoTracking();
        IQueryable<Asset> assets = db.Assets.AsNoTracking();
        if (scope == "store") { tickets = tickets.Where(t => t.StoreId == storeId); assets = assets.Where(a => a.StoreId == storeId); }
        if (scope == "user") { tickets = tickets.Where(t => t.RequesterId == userId); assets = assets.Where(a => a.AssignedUserId == userId); }

        var byStatus = await tickets.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int S(TicketStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;

        var byPriority = await tickets.Where(t => t.Status != TicketStatus.Closed)
            .GroupBy(t => t.Priority).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        var byCategory = await tickets.GroupBy(t => new { t.CategoryId, t.Category.Name, t.Category.Color })
            .Select(g => new { g.Key.Name, g.Key.Color, Count = g.Count() }).OrderByDescending(x => x.Count).Take(8).ToListAsync(ct);

        var since = DateTime.UtcNow.Date.AddDays(-13);
        var created = await tickets.Where(t => t.CreatedAt >= since).Select(t => t.CreatedAt).ToListAsync(ct);
        var resolved = await tickets.Where(t => t.ResolvedAt != null && t.ResolvedAt >= since).Select(t => t.ResolvedAt!.Value).ToListAsync(ct);
        var days = Enumerable.Range(0, 14).Select(i => DateOnly.FromDateTime(since.AddDays(i)))
            .Select(d => new DailyPoint(d, created.Count(x => DateOnly.FromDateTime(x) == d), resolved.Count(x => DateOnly.FromDateTime(x) == d))).ToList();

        var pairs = await tickets.Where(t => t.ResolvedAt != null && t.CreatedAt >= DateTime.UtcNow.AddDays(-90))
            .Select(t => new { t.CreatedAt, ResolvedAt = t.ResolvedAt!.Value }).ToListAsync(ct);
        double? avgHours = pairs.Count == 0 ? null : Math.Round(pairs.Average(p => (p.ResolvedAt - p.CreatedAt).TotalHours), 1);

        var topAgents = new List<CountItem>();
        var byStore = new List<CountItem>();
        if (scope == "all")
        {
            topAgents = (await tickets.Where(t => t.AssigneeId != null && t.Status != TicketStatus.Closed)
                    .GroupBy(t => new { t.AssigneeId, t.Assignee!.FullName }).Select(g => new { g.Key.AssigneeId, g.Key.FullName, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(5).ToListAsync(ct))
                .Select(a => new CountItem(a.AssigneeId!.Value.ToString(), a.FullName, a.Count)).ToList();
            byStore = (await tickets.Where(t => t.StoreId != null && t.Status != TicketStatus.Closed)
                    .GroupBy(t => new { t.StoreId, t.Store!.Name }).Select(g => new { g.Key.StoreId, g.Key.Name, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(8).ToListAsync(ct))
                .Select(s => new CountItem(s.StoreId!.Value.ToString(), s.Name, s.Count)).ToList();
        }

        var recent = await tickets.OrderByDescending(t => t.CreatedAt).Take(6).ToListItems(scope == "all").ToListAsync(ct);
        var scopeLabel = scope switch
        {
            "all" => "Tüm şirket",
            "store" => await db.Stores.Where(s => s.Id == storeId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? "Mağazanız",
            _ => "Talepleriniz",
        };

        return new DashboardStatsDto(scope, scopeLabel,
            byStatus.Sum(x => x.Count), S(TicketStatus.Open), S(TicketStatus.InProgress), S(TicketStatus.Pending), S(TicketStatus.Resolved), S(TicketStatus.Closed),
            await tickets.CountAsync(t => t.AssigneeId == null && t.Status != TicketStatus.Closed, ct),
            await tickets.CountAsync(t => t.Priority == TicketPriority.Critical && t.Status != TicketStatus.Closed, ct),
            avgHours,
            Enum.GetValues<TicketStatus>().Select(s => new CountItem(s.ToString(), StatusLabel(s), S(s))).ToList(),
            Enum.GetValues<TicketPriority>().Select(p => new CountItem(p.ToString(), PriorityLabel(p), byPriority.FirstOrDefault(x => x.Key == p)?.Count ?? 0)).ToList(),
            byCategory.Select(c => new CountItem(c.Name, c.Name, c.Count, c.Color)).ToList(),
            byStore, days, topAgents, recent,
            await assets.CountAsync(ct),
            scope == "all" ? await db.Users.CountAsync(u => u.IsActive, ct) : scope == "store" ? await db.Users.CountAsync(u => u.StoreId == storeId && u.IsActive, ct) : 0,
            scope == "all" ? await db.Stores.CountAsync(s => s.IsActive, ct) : 0,
            DateTime.UtcNow);
    }

    private static string StatusLabel(TicketStatus s) => s switch
    {
        TicketStatus.Open => "Açık",
        TicketStatus.InProgress => "İşlemde",
        TicketStatus.Pending => "Beklemede",
        TicketStatus.Resolved => "Çözüldü",
        _ => "Kapandı",
    };

    private static string PriorityLabel(TicketPriority p) => p switch
    {
        TicketPriority.Low => "Düşük",
        TicketPriority.Medium => "Orta",
        TicketPriority.High => "Yüksek",
        _ => "Kritik",
    };
}
