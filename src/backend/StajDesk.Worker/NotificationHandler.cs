using Microsoft.EntityFrameworkCore;
using StajDesk.Contracts;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;
using StajDesk.Infrastructure.Persistence;
using StajDesk.Infrastructure.Realtime;

namespace StajDesk.Worker;

/// <summary>
/// Bir olaydan kimin hangi bildirimi / e-postayı alacağına karar verir.
/// Idempotent: aynı olay (EventId) ikinci kez gelirse işlenmez (en az bir kez teslimde çift bildirim olmaz).
/// </summary>
public sealed class NotificationHandler(AppDbContext db, Mailer mailer, RealtimePublisher realtime, ILogger<NotificationHandler> logger)
{
    private static readonly Dictionary<string, string> StatusTr = new()
    {
        ["New"] = "Yeni", ["Rejected"] = "Reddedildi", ["Open"] = "Açık", ["InProgress"] = "İşlemde", ["Pending"] = "Beklemede", ["Resolved"] = "Çözüldü", ["Closed"] = "Kapandı",
    };

    public async Task HandleAsync(IntegrationEvent evt, CancellationToken ct)
    {
        if (await db.ProcessedMessages.AnyAsync(m => m.MessageId == evt.EventId, ct))
        {
            logger.LogInformation("Olay daha önce işlenmiş, atlanıyor: {EventId}", evt.EventId);
            return;
        }

        switch (evt)
        {
            case TicketCreatedEvent e: await OnCreatedAsync(e, ct); break;
            case TicketStatusChangedEvent e: await OnStatusChangedAsync(e, ct); break;
            case TicketAssignedEvent e: await OnAssignedAsync(e, ct); break;
            case TicketCommentAddedEvent e: await OnCommentAddedAsync(e, ct); break;
            case UserRegisteredEvent e: await OnRegisteredAsync(e, ct); break;
            case PasswordResetRequestedEvent e: await OnPasswordResetAsync(e, ct); break;
            case UserInvitedEvent e: await OnInvitedAsync(e, ct); break;
        }

        var added = db.ChangeTracker.Entries<Notification>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();

        db.ProcessedMessages.Add(new ProcessedMessage { MessageId = evt.EventId });
        await db.SaveChangesAsync(ct);

        // Kayıt kalıcı olduktan sonra tarayıcılara "yeni bildirim var" sinyali (SignalR köprüsü üzerinden).
        foreach (var n in added.DistinctBy(n => n.UserId))
            await realtime.PublishAsync(new RealtimeMessage([n.UserId], "notification", n.TicketId, n.Title));
    }

    private string TicketUrl(int id) => $"{mailer.WebUrl}/tickets/{id}";

    private async Task OnCreatedAsync(TicketCreatedEvent e, CancellationToken ct)
    {
        // Destek ekibi (talepleri yönetebilenler) + talebin mağazasının sahibi bilgilendirilir.
        var staff = await db.Users.Where(u => u.IsActive && u.Id != e.RequesterId &&
                u.Roles.Any(r => r.Role.Permissions.Any(p => p.Permission.Key == Permissions.TicketsManage)))
            .Select(u => new { u.Id, u.Email, WantsEmail = u.Settings == null || u.Settings.EmailTicketUpdates }).ToListAsync(ct);
        var owners = e.StoreId is null ? [] : await db.Users.Where(u => u.IsActive && u.Id != e.RequesterId && u.StoreId == e.StoreId &&
                u.Roles.Any(r => r.Role.Permissions.Any(p => p.Permission.Key == Permissions.TicketsReadStore)) &&
                !u.Roles.Any(r => r.Role.Permissions.Any(p => p.Permission.Key == Permissions.TicketsManage)))
            .Select(u => new { u.Id, u.Email, WantsEmail = u.Settings == null || u.Settings.EmailTicketUpdates }).ToListAsync(ct);

        foreach (var s in staff.Concat(owners).DistinctBy(x => x.Id))
            Notify(s.Id, e.TicketId, $"Yeni talep #{e.TicketId}", $"{e.RequesterName} yeni bir talep açtı: \"{e.Title}\" ({e.CategoryName}, öncelik: {e.Priority})");
        Notify(e.RequesterId, e.TicketId, $"Talebiniz alındı #{e.TicketId}", $"\"{e.Title}\" başlıklı talebiniz destek ekibine iletildi.");

        var requester = await EmailOf(e.RequesterId, ct);
        if (requester is not null)
            await mailer.SendAsync(requester, $"Talebiniz alındı (#{e.TicketId})", "ticket.created", "Talebiniz alındı",
                [$"\"{e.Title}\" başlıklı talebiniz destek ekibine iletildi.", "Durum değiştiğinde size haber vereceğiz."], ("Talebi görüntüle", TicketUrl(e.TicketId)), ct);
        foreach (var s in staff.Where(x => x.WantsEmail))
            await mailer.SendAsync(s.Email, $"Yeni talep #{e.TicketId}: {e.Title}", "ticket.created.staff", "Yeni destek talebi",
                [$"{e.RequesterName} yeni bir talep açtı: \"{e.Title}\"", $"Kategori: {e.CategoryName} · Öncelik: {e.Priority}"], ("Talebi incele", TicketUrl(e.TicketId)), ct);
    }

    private async Task OnStatusChangedAsync(TicketStatusChangedEvent e, CancellationToken ct)
    {
        var text = $"\"{e.Title}\" talebinin durumu {Tr(e.OldStatus)} → {Tr(e.NewStatus)} olarak güncellendi ({e.ChangedByName}).";
        if (e.RequesterId != e.ChangedById)
        {
            Notify(e.RequesterId, e.TicketId, $"Talep #{e.TicketId}: {Tr(e.NewStatus)}", text);
            var to = await EmailOf(e.RequesterId, ct);
            if (to is not null)
                await mailer.SendAsync(to, $"Talebinizin durumu: {Tr(e.NewStatus)} (#{e.TicketId})", "ticket.status", $"Talep durumu: {Tr(e.NewStatus)}",
                    [text, e.NewStatus == "Resolved" ? "Sorununuz çözüldüyse talebi onaylayıp kapatabilirsiniz." : ""], ("Talebi görüntüle", TicketUrl(e.TicketId)), ct);
        }
        if (e.AssigneeId is { } a && a != e.ChangedById && a != e.RequesterId)
            Notify(a, e.TicketId, $"Talep #{e.TicketId}: {Tr(e.NewStatus)}", text);
    }

    private async Task OnAssignedAsync(TicketAssignedEvent e, CancellationToken ct)
    {
        if (e.AssigneeId is { } a && a != e.AssignedById)
        {
            Notify(a, e.TicketId, $"Size talep atandı #{e.TicketId}", $"\"{e.Title}\" talebi size atandı.");
            var to = await EmailOf(a, ct);
            if (to is not null)
                await mailer.SendAsync(to, $"Size talep atandı (#{e.TicketId})", "ticket.assigned", "Size yeni bir talep atandı",
                    [$"\"{e.Title}\" talebi size atandı."], ("Talebi aç", TicketUrl(e.TicketId)), ct);
        }
        if (e.AssigneeId is not null && e.RequesterId != e.AssignedById)
            Notify(e.RequesterId, e.TicketId, $"Talep #{e.TicketId} ilgilenen kişi", $"\"{e.Title}\" talebinizle {e.AssigneeName} ilgileniyor.");
    }

    private async Task OnCommentAddedAsync(TicketCommentAddedEvent e, CancellationToken ct)
    {
        // Dahili notlar talep sahibine bildirilmez.
        if (!e.IsInternal && e.RequesterId != e.AuthorId)
        {
            Notify(e.RequesterId, e.TicketId, $"Yeni yanıt #{e.TicketId}", $"{e.AuthorName}, \"{e.Title}\" talebinize yanıt yazdı.");
            var to = await EmailOf(e.RequesterId, ct);
            if (to is not null)
                await mailer.SendAsync(to, $"Talebinize yanıt var (#{e.TicketId})", "ticket.comment", "Talebinize yeni yanıt",
                    [$"{e.AuthorName}, \"{e.Title}\" talebinize yanıt yazdı."], ("Yanıtı oku", TicketUrl(e.TicketId)), ct);
        }
        if (e.AssigneeId is { } a && a != e.AuthorId)
            Notify(a, e.TicketId, $"Yeni yorum #{e.TicketId}", $"{e.AuthorName}, \"{e.Title}\" talebine yorum ekledi.");
    }

    private Task OnRegisteredAsync(UserRegisteredEvent e, CancellationToken ct) => e.VerifyUrl is null
        ? mailer.SendAsync(e.Email, "StajDesk'e hoş geldiniz", "user.welcome", $"Hoş geldiniz, {e.FullName}",
            ["Hesabınız oluşturuldu. Bir sorun yaşadığınızda StajDesk üzerinden talep açabilir, durumunu takip edebilirsiniz."],
            ("StajDesk'i aç", mailer.WebUrl), ct)
        : mailer.SendAsync(e.Email, "E-posta adresinizi doğrulayın", "user.verify_email", $"Hoş geldiniz, {e.FullName}",
            ["Hesabınız oluşturuldu. Giriş yapabilmek için e-posta adresinizi doğrulayın.", "Bağlantı 24 saat geçerlidir. Bu kaydı siz yapmadıysanız e-postayı yok sayabilirsiniz."],
            ("E-postamı doğrula", e.VerifyUrl), ct);

    private Task OnInvitedAsync(UserInvitedEvent e, CancellationToken ct) =>
        mailer.SendAsync(e.Email, "StajDesk hesabınız hazır", "user.invited", $"Merhaba {e.FullName}",
            [$"{e.InvitedBy} sizin için bir StajDesk hesabı oluşturdu.", $"Aşağıdaki bağlantıdan ilk şifrenizi belirleyin. Bağlantı {e.ExpiresInHours} saat geçerlidir ve yalnızca bir kez kullanılabilir."],
            ("Şifremi belirle", e.SetPasswordUrl), ct);

    private Task OnPasswordResetAsync(PasswordResetRequestedEvent e, CancellationToken ct) =>
        mailer.SendAsync(e.Email, "Şifre sıfırlama bağlantınız", "user.password_reset", "Şifrenizi sıfırlayın",
            [$"Merhaba {e.FullName},", $"Şifrenizi sıfırlamak için aşağıdaki bağlantıyı kullanın. Bağlantı {e.ExpiresInMinutes} dakika geçerlidir ve yalnızca bir kez kullanılabilir.",
             "Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz; şifreniz değişmez."],
            ("Şifremi sıfırla", e.ResetUrl), ct);

    private void Notify(int userId, int ticketId, string title, string message) =>
        db.Notifications.Add(new Notification { UserId = userId, TicketId = ticketId, Title = Truncate(title, 200), Message = Truncate(message, 1000) });

    /// <summary>Talep e-postaları kullanıcının bildirim tercihine uyar (uygulama içi bildirim her zaman oluşur).</summary>
    private Task<string?> EmailOf(int userId, CancellationToken ct) =>
        db.Users.Where(u => u.Id == userId && u.IsActive && (u.Settings == null || u.Settings.EmailTicketUpdates))
            .Select(u => u.Email).FirstOrDefaultAsync(ct);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    private static string Tr(string status) => StatusTr.GetValueOrDefault(status, status);
}
