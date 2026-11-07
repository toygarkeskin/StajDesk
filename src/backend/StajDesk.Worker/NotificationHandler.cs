using Microsoft.EntityFrameworkCore;
using StajDesk.Contracts;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Infrastructure.Persistence;

namespace StajDesk.Worker;

/// <summary>Bir olaydan kimin, hangi bildirimi alacağına karar verir ve notifications tablosuna yazar.</summary>
public sealed class NotificationHandler(AppDbContext db, ILogger<NotificationHandler> logger)
{
    private static readonly Dictionary<string, string> StatusTr = new()
    {
        ["Open"] = "Açık", ["InProgress"] = "İşlemde", ["Resolved"] = "Çözüldü", ["Closed"] = "Kapandı"
    };

    public async Task HandleAsync(IntegrationEvent evt, CancellationToken ct)
    {
        var notifications = evt switch
        {
            TicketCreatedEvent e => await OnCreatedAsync(e, ct),
            TicketStatusChangedEvent e => OnStatusChanged(e),
            TicketAssignedEvent e => OnAssigned(e),
            TicketCommentAddedEvent e => OnCommentAdded(e),
            _ => []
        };
        if (notifications.Count == 0) return;

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);

        // E-posta gönderimi şimdilik simüle ediliyor (bonus görev: MailHog ile gerçek e-posta).
        var userIds = notifications.Select(n => n.UserId).Distinct().ToList();
        var emails = await db.Users.Where(u => userIds.Contains(u.Id)).Select(u => u.Email).ToListAsync(ct);
        foreach (var email in emails)
            logger.LogInformation("E-posta gönderildi (simülasyon) → {Email}: {Title}", email, notifications[0].Title);

        logger.LogInformation("{Count} bildirim oluşturuldu ({EventType}, talep #{TicketId})",
            notifications.Count, evt.GetType().Name, notifications[0].TicketId);
    }

    private async Task<List<Notification>> OnCreatedAsync(TicketCreatedEvent e, CancellationToken ct)
    {
        var staffIds = await db.Users
            .Where(u => u.IsActive && (u.Role == UserRole.Agent || u.Role == UserRole.Admin) && u.Id != e.RequesterId)
            .Select(u => u.Id).ToListAsync(ct);

        var list = staffIds.Select(id => New(id, e.TicketId, $"Yeni talep #{e.TicketId}",
            $"{e.RequesterName} yeni bir talep açtı: \"{e.Title}\" ({e.CategoryName}, öncelik: {e.Priority})")).ToList();
        list.Add(New(e.RequesterId, e.TicketId, $"Talebiniz alındı #{e.TicketId}",
            $"\"{e.Title}\" başlıklı talebiniz destek ekibine iletildi."));
        return list;
    }

    private static List<Notification> OnStatusChanged(TicketStatusChangedEvent e)
    {
        var list = new List<Notification>();
        var text = $"\"{e.Title}\" talebinin durumu {Tr(e.OldStatus)} → {Tr(e.NewStatus)} olarak güncellendi ({e.ChangedByName}).";
        if (e.RequesterId != e.ChangedById)
            list.Add(New(e.RequesterId, e.TicketId, $"Talep #{e.TicketId}: {Tr(e.NewStatus)}", text));
        if (e.AssigneeId is { } a && a != e.ChangedById && a != e.RequesterId)
            list.Add(New(a, e.TicketId, $"Talep #{e.TicketId}: {Tr(e.NewStatus)}", text));
        return list;
    }

    private static List<Notification> OnAssigned(TicketAssignedEvent e)
    {
        var list = new List<Notification>();
        if (e.AssigneeId is { } a && a != e.AssignedById)
            list.Add(New(a, e.TicketId, $"Size talep atandı #{e.TicketId}", $"\"{e.Title}\" talebi size atandı."));
        if (e.AssigneeId is not null && e.RequesterId != e.AssignedById)
            list.Add(New(e.RequesterId, e.TicketId, $"Talep #{e.TicketId} ilgilenen kişi",
                $"\"{e.Title}\" talebinizle {e.AssigneeName} ilgileniyor."));
        return list;
    }

    private static List<Notification> OnCommentAdded(TicketCommentAddedEvent e)
    {
        var list = new List<Notification>();
        // Dahili notlar talep sahibine bildirilmez.
        if (!e.IsInternal && e.RequesterId != e.AuthorId)
            list.Add(New(e.RequesterId, e.TicketId, $"Yeni yanıt #{e.TicketId}", $"{e.AuthorName}, \"{e.Title}\" talebinize yanıt yazdı."));
        if (e.AssigneeId is { } a && a != e.AuthorId)
            list.Add(New(a, e.TicketId, $"Yeni yorum #{e.TicketId}", $"{e.AuthorName}, \"{e.Title}\" talebine yorum ekledi."));
        return list;
    }

    private static Notification New(int userId, int ticketId, string title, string message) =>
        new() { UserId = userId, TicketId = ticketId, Title = Truncate(title, 200), Message = Truncate(message, 1000) };

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    private static string Tr(string status) => StatusTr.GetValueOrDefault(status, status);
}
