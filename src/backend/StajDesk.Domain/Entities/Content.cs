using StajDesk.Domain.Common;
using StajDesk.Domain.Enums;

namespace StajDesk.Domain.Entities;

/// <summary>Kurumsal sitedeki haber. Yalnızca yayımlanmış ve yayın tarihi gelmiş haberler herkese açıktır.</summary>
public class NewsArticle : BaseEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ImageKey { get; set; }
    public bool IsPublished { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;
}

/// <summary>
/// Reklam / banner. Yalnızca aktif ve tarih aralığı içindekiler gösterilir: süresi dolan reklam
/// ek bir işlem gerekmeden yayından kalkar. Gösterim ve tıklamalar gün bazında <see cref="AdStatDaily"/>'de toplanır.
/// </summary>
public class Advertisement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ImageKey { get; set; } = string.Empty;
    /// <summary>Yalnızca http/https; tıklama sunucu üzerinden sayılıp bu adrese yönlendirilir.</summary>
    public string? LinkUrl { get; set; }
    public AdPage Page { get; set; } = AdPage.Landing;
    public AdPlacement Placement { get; set; } = AdPlacement.Hero;
    public int SortOrder { get; set; }
    public DateTime StartsAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<AdStatDaily> Stats { get; set; } = new List<AdStatDaily>();

    public bool IsLive(DateTime now) => IsActive && StartsAt <= now && (EndsAt is null || EndsAt > now);
}

public class AdStatDaily
{
    public int AdvertisementId { get; set; }
    public Advertisement Advertisement { get; set; } = null!;
    public DateOnly Day { get; set; }
    public int Impressions { get; set; }
    public int Clicks { get; set; }
}

/// <summary>Kariyer sayfasındaki açık pozisyon.</summary>
public class JobOpening : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string Location { get; set; } = string.Empty;
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public string Description { get; set; } = string.Empty;
    public string? Requirements { get; set; }
    public bool IsPublished { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public DateOnly? ClosesOn { get; set; }
}

/// <summary>Kurumsal site metinleri ve ayarları (anahtar → değer). Ör. "about.body", "contact.email", "links".</summary>
public class SiteSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Herkese açık site için yüklenen görsel (haber, reklam). Talep ekleri ve personel fotoğraflarından ayrıdır:
/// herkese açık uç yalnızca bu tablodaki kayıtları sunar.
/// </summary>
public class MediaFile
{
    public string Key { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int UploadedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Dış kaynaktan (hava, kur) alınan son BAŞARILI veri. Kaynak geçici çökse bile "son güncelleme" ile gösterilir.</summary>
public class ExternalSnapshot
{
    public string Key { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; }
}
