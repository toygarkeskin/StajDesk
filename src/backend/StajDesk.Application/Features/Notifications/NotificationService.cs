using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;

namespace StajDesk.Application.Features.Notifications;

public sealed record NotificationDto(int Id, string Title, string Message, int? TicketId, bool IsRead, DateTime CreatedAt, string? Link = null);

public sealed record NotificationQuery : PagedQuery
{
    public bool? UnreadOnly { get; init; }
}

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetMineAsync(NotificationQuery query, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkReadAsync(int id, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(CancellationToken ct = default);
}

internal sealed class NotificationService(IAppDbContext db, ICurrentUser currentUser) : INotificationService
{
    public async Task<PagedResult<NotificationDto>> GetMineAsync(NotificationQuery query, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var q = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (query.UnreadOnly == true) q = q.Where(n => !n.IsRead);
        return await q.OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.TicketId, n.IsRead, n.CreatedAt, n.Link))
            .ToPagedResultAsync(query, ct);
    }

    public Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        return db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task MarkReadAsync(int id, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
                ?? throw NotFoundException.For("Bildirim", id);
        if (n.IsRead) return;
        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<int> MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = DateTime.UtcNow;
        return db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, now), ct);
    }
}
