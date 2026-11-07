using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Tickets;

/// <summary>
/// Talep görünürlüğü ve durum geçiş yetkileri — tek yerde.
///  - tickets.read.all   → tüm talepler
///  - tickets.read.store → kendi mağazasındaki talepler
///  - tickets.read.own   → yalnızca kendi açtığı talepler
/// </summary>
public static class TicketAccess
{
    public static IQueryable<Ticket> Visible(this IQueryable<Ticket> tickets, ICurrentUser user)
    {
        user.Require(Permissions.TicketsReadAll, Permissions.TicketsReadStore, Permissions.TicketsReadOwn);
        if (user.Has(Permissions.TicketsReadAll)) return tickets;

        var userId = user.Has(Permissions.TicketsReadOwn) ? user.UserId : null;
        var storeId = user.Has(Permissions.TicketsReadStore) ? user.StoreId : null;
        return tickets.Where(t => (userId != null && t.RequesterId == userId) || (storeId != null && t.StoreId == storeId));
    }

    public static bool CanSee(ICurrentUser user, Ticket t) =>
        user.Has(Permissions.TicketsReadAll)
        || (user.Has(Permissions.TicketsReadOwn) && t.RequesterId == user.UserId)
        || (user.Has(Permissions.TicketsReadStore) && user.StoreId != null && t.StoreId == user.StoreId);

    public static void EnsureCanSee(ICurrentUser user, Ticket t)
    {
        if (!CanSee(user, t)) throw new ForbiddenException("Bu talebi görüntüleme yetkiniz yok.");
    }

    /// <summary>Mevcut kullanıcının talebi hangi durumlara geçirebileceği (arayüz butonları da bunu kullanır).</summary>
    public static IReadOnlyList<TicketStatus> AllowedStatuses(ICurrentUser user, Ticket t)
    {
        var all = Enum.GetValues<TicketStatus>().Where(s => s != t.Status && Ticket.CanTransition(t.Status, s)).ToList();
        if (user.Has(Permissions.TicketsManage)) return all;
        // Talep sahibi: çözülen talebi onaylayıp kapatabilir veya "çözülmedi" diyerek yeniden açabilir.
        if (t.RequesterId == user.UserId && t.Status == TicketStatus.Resolved)
            return all.Where(s => s is TicketStatus.Closed or TicketStatus.InProgress).ToList();
        return [];
    }

    public static bool CanEdit(ICurrentUser user, Ticket t) =>
        user.Has(Permissions.TicketsManage)
            ? t.Status != TicketStatus.Closed
            : user.Has(Permissions.TicketsUpdateOwn) && t.RequesterId == user.UserId && t.Status == TicketStatus.Open;
}
