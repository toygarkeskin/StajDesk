using StajDesk.Domain.Common;
using StajDesk.Domain.Enums;

namespace StajDesk.Domain.Entities;

/// <summary>
/// Panelde gösterilen duyuru. <see cref="StoreId"/> boşsa herkese, doluysa yalnızca o mağazaya görünür.
/// Yayın tarihi gelmemiş veya süresi geçmiş duyurular listelenmez.
/// </summary>
public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public AnnouncementLevel Level { get; set; } = AnnouncementLevel.Info;
    public bool IsPinned { get; set; }
    public DateTime PublishAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    public int? StoreId { get; set; }
    public Store? Store { get; set; }

    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;
}

/// <summary>Bilgi işlem nöbet çizelgesi: belirli tarih aralığında acil durumlarda aranacak destek personeli.</summary>
public class OnCallShift : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string? Note { get; set; }

    public bool Covers(DateOnly day) => day >= StartsOn && day <= EndsOn;
}

/// <summary>
/// Talebe eklenen dosya. Dosyanın kendisi diskte (Docker volume) durur; burada yalnızca üst bilgisi tutulur.
/// <see cref="StorageKey"/> rastgele üretilir; kullanıcının verdiği dosya adı diskte yol olarak asla kullanılmaz.
/// </summary>
public class TicketAttachment : BaseEntity
{
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string StorageKey { get; set; } = string.Empty;

    public int UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
}
