using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Tickets;

/* =========================================================
   Talep ekleri (dosya yükleme).
   Güvenlik:
   - Boyut (5 MB) ve sayı (talep başına 10) sınırı
   - Uzantı izin listesi + dosyanın ilk baytlarına bakarak tür doğrulama (uzantısı .png olan bir .exe reddedilir)
   - İçerik türünü istemci değil sunucu belirler; indirme her zaman "attachment" olarak yapılır (tarayıcıda çalıştırılmaz)
   - Diskteki ad rastgele üretilir; kullanıcının dosya adı yol olarak kullanılmaz (path traversal yok)
   ========================================================= */

public sealed record TicketAttachmentDto(int Id, string FileName, string ContentType, long Size, UserSummaryDto UploadedBy, DateTime CreatedAt, bool CanDelete);

public sealed record UploadAttachmentCommand(int TicketId, string FileName, long Length, Stream Content) : ICommand<TicketAttachmentDto>;
public sealed record DeleteAttachmentCommand(int TicketId, int AttachmentId) : ICommand<bool>;
public sealed record DownloadAttachmentQuery(int TicketId, int AttachmentId) : IQuery<AttachmentFile>;
public sealed record AttachmentFile(Stream Content, string ContentType, string FileName);

public static class AttachmentRules
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const int MaxPerTicket = 10;

    /// <summary>İzin verilen uzantılar → sunucunun döneceği içerik türü.</summary>
    public static readonly IReadOnlyDictionary<string, string> Types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain", [".log"] = "text/plain", [".csv"] = "text/csv",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    public static string AllowedList => string.Join(", ", Types.Keys);

    /// <summary>Dosyanın ilk baytları uzantısıyla uyumlu mu? (magic number kontrolü)</summary>
    public static bool SignatureMatches(string extension, ReadOnlySpan<byte> head)
    {
        static bool Starts(ReadOnlySpan<byte> h, params byte[] sig) => h.Length >= sig.Length && h[..sig.Length].SequenceEqual(sig);
        return extension.ToLowerInvariant() switch
        {
            ".png" => Starts(head, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            ".jpg" or ".jpeg" => Starts(head, 0xFF, 0xD8, 0xFF),
            ".gif" => Starts(head, (byte)'G', (byte)'I', (byte)'F', (byte)'8'),
            ".webp" => head.Length >= 12 && Starts(head, (byte)'R', (byte)'I', (byte)'F', (byte)'F') && head[8..12].SequenceEqual("WEBP"u8),
            ".pdf" => Starts(head, (byte)'%', (byte)'P', (byte)'D', (byte)'F'),
            ".docx" or ".xlsx" => Starts(head, 0x50, 0x4B, 0x03, 0x04),
            // Metin dosyalarında NUL baytı olmamalı (ikili dosya metin gibi yüklenemez)
            ".txt" or ".log" or ".csv" => !head.Contains((byte)0),
            _ => false,
        };
    }

    /// <summary>Gösterim için güvenli dosya adı: yol parçaları, kontrol ve özel karakterler atılır.</summary>
    public static string SafeFileName(string name)
    {
        var file = Path.GetFileName(name.Replace('\\', '/'));
        var chars = file.Where(c => !char.IsControl(c) && "<>:\"/\\|?*".IndexOf(c) < 0).ToArray();
        var clean = new string(chars).Trim().Trim('.');
        if (clean.Length == 0) clean = "dosya";
        if (clean.Length > 120)
        {
            var ext = Path.GetExtension(clean);
            clean = clean[..(120 - ext.Length)] + ext;
        }
        return clean;
    }
}

internal static class AttachmentAccess
{
    public static bool CanDelete(ICurrentUser user, Ticket t, TicketAttachment a) =>
        user.Has(Permissions.TicketsManage) || (a.UploadedById == user.UserId && (t.Status != TicketStatus.Closed && t.Status != TicketStatus.Rejected));

    public static bool CanUpload(ICurrentUser user, Ticket t) =>
        (t.Status != TicketStatus.Closed && t.Status != TicketStatus.Rejected) && (user.Has(Permissions.TicketsManage) || (user.Has(Permissions.CommentsCreate) && TicketAccess.CanSee(user, t)));
}

internal sealed class UploadAttachmentHandler(IAppDbContext db, ICurrentUser user, IFileStorage storage, IAuditLogger audit)
    : IRequestHandler<UploadAttachmentCommand, TicketAttachmentDto>
{
    public async Task<TicketAttachmentDto> HandleAsync(UploadAttachmentCommand c, CancellationToken ct)
    {
        var userId = user.RequireUserId();
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == c.TicketId, ct) ?? throw NotFoundException.For("Talep", c.TicketId);
        TicketAccess.EnsureCanSee(user, ticket);
        if (!AttachmentAccess.CanUpload(user, ticket))
            throw new ForbiddenException(ticket.IsFinished ? "Kapanmış veya reddedilmiş talebe dosya eklenemez." : "Bu talebe dosya ekleme yetkiniz yok.");

        var fileName = AttachmentRules.SafeFileName(c.FileName);
        var ext = Path.GetExtension(fileName);
        if (!AttachmentRules.Types.TryGetValue(ext, out var contentType))
            throw new DomainException($"Bu dosya türü desteklenmiyor. İzin verilenler: {AttachmentRules.AllowedList}");
        if (c.Length <= 0) throw new DomainException("Dosya boş görünüyor. Başka bir dosya seçin.");
        if (c.Length > AttachmentRules.MaxBytes) throw new DomainException("Dosya 5 MB'tan büyük olamaz. Dosyayı küçültüp tekrar deneyin.");
        if (await db.TicketAttachments.CountAsync(a => a.TicketId == ticket.Id, ct) >= AttachmentRules.MaxPerTicket)
            throw new DomainException($"Bir talebe en fazla {AttachmentRules.MaxPerTicket} dosya eklenebilir. Gereksiz olanları silip tekrar deneyin.");

        // İçeriği belleğe al (en fazla 5 MB) ve gerçek boyutu / imzayı doğrula — istemcinin bildirdiği boyuta güvenme.
        using var buffer = new MemoryStream();
        await c.Content.CopyToAsync(buffer, ct);
        if (buffer.Length > AttachmentRules.MaxBytes) throw new DomainException("Dosya 5 MB'tan büyük olamaz. Dosyayı küçültüp tekrar deneyin.");
        if (buffer.Length == 0) throw new DomainException("Dosya boş görünüyor. Başka bir dosya seçin.");
        var head = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 4096));
        if (!AttachmentRules.SignatureMatches(ext, head))
            throw new DomainException("Dosyanın içeriği uzantısıyla uyuşmuyor. Dosyayı orijinal biçiminde yükleyin.");

        var key = Guid.NewGuid().ToString("N");
        buffer.Position = 0;
        await storage.SaveAsync(key, buffer, ct);

        var attachment = new TicketAttachment
        {
            TicketId = ticket.Id, FileName = fileName, ContentType = contentType, Size = buffer.Length, StorageKey = key, UploadedById = userId,
        };
        db.TicketAttachments.Add(attachment);
        db.TicketHistories.Add(new TicketHistory { TicketId = ticket.Id, ActorId = userId, Action = TicketHistoryAction.AttachmentAdded, NewValue = Truncate(fileName) });
        ticket.UpdatedAt = DateTime.UtcNow;
        audit.Add("ticket.attachment_added", "ticket", ticket.Id, new { fileName, size = buffer.Length });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None); // kayıt oluşmadıysa diskte yetim dosya kalmasın
            throw;
        }

        var me = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new UserSummaryDto(u.Id, u.FullName, u.Email)).FirstAsync(ct);
        return new TicketAttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.Size, me, attachment.CreatedAt, true);
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..199] + "…";
}

internal sealed class DeleteAttachmentHandler(IAppDbContext db, ICurrentUser user, IFileStorage storage, IAuditLogger audit)
    : IRequestHandler<DeleteAttachmentCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteAttachmentCommand c, CancellationToken ct)
    {
        var userId = user.RequireUserId();
        var a = await db.TicketAttachments.Include(x => x.Ticket).FirstOrDefaultAsync(x => x.Id == c.AttachmentId && x.TicketId == c.TicketId, ct)
                ?? throw NotFoundException.For("Dosya", c.AttachmentId);
        TicketAccess.EnsureCanSee(user, a.Ticket);
        if (!AttachmentAccess.CanDelete(user, a.Ticket, a)) throw new ForbiddenException("Bu dosyayı yalnızca yükleyen kişi veya destek ekibi silebilir.");

        db.TicketAttachments.Remove(a);
        db.TicketHistories.Add(new TicketHistory { TicketId = a.TicketId, ActorId = userId, Action = TicketHistoryAction.AttachmentRemoved, OldValue = a.FileName });
        audit.Add("ticket.attachment_removed", "ticket", a.TicketId, new { a.FileName });
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(a.StorageKey, ct);
        return true;
    }
}

internal sealed class DownloadAttachmentHandler(IAppDbContext db, ICurrentUser user, IFileStorage storage) : IRequestHandler<DownloadAttachmentQuery, AttachmentFile>
{
    public async Task<AttachmentFile> HandleAsync(DownloadAttachmentQuery q, CancellationToken ct)
    {
        var a = await db.TicketAttachments.AsNoTracking().Include(x => x.Ticket)
                    .FirstOrDefaultAsync(x => x.Id == q.AttachmentId && x.TicketId == q.TicketId, ct)
                ?? throw NotFoundException.For("Dosya", q.AttachmentId);
        TicketAccess.EnsureCanSee(user, a.Ticket);
        var stream = await storage.OpenReadAsync(a.StorageKey, ct)
                     ?? throw new NotFoundException("Dosya sunucuda bulunamadı. Destek ekibine bildirin.");
        return new AttachmentFile(stream, a.ContentType, a.FileName);
    }
}
