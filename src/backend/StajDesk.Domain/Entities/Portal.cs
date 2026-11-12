using StajDesk.Domain.Common;

namespace StajDesk.Domain.Entities;

/// <summary>
/// Panel kutucuğunun (widget) şirket genelindeki ayarı. Kutucukların kendisi kodda tanımlıdır (katalog);
/// yönetici burada açık/kapalı, sıra, boyut ve hangi rollerin göreceğini belirler. Satır yoksa katalog varsayılanı geçerlidir.
/// </summary>
public class DashboardWidgetConfig
{
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public WidgetSize Size { get; set; } = WidgetSize.Small;
    /// <summary>Boşsa herkes görür (izni olan herkes). Doluysa yalnızca bu rollerden birine sahip olanlar.</summary>
    public List<string> Roles { get; set; } = [];
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Kullanıcının kendi panel düzeni: sırası ve gizlediği kutucuklar.</summary>
public class UserDashboardPreference
{
    public int UserId { get; set; }
    public string WidgetKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool Hidden { get; set; }
}

public enum WidgetSize { Small = 1, Medium = 2, Large = 3, Full = 4 }

/// <summary>Şirket etkinliği (eğitim, toplantı, kutlama...). Portal panelinde yaklaşanlar gösterilir.</summary>
public class CompanyEvent : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsPublished { get; set; } = true;
    public int CreatedById { get; set; }
}

public enum RecognitionType { Achievement = 1, EmployeeOfWeek = 2 }

/// <summary>Çalışan takdiri: başarı veya haftanın çalışanı. Yalnızca giriş sonrası portalda görünür (kişisel veri).</summary>
public class Recognition : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public RecognitionType Type { get; set; } = RecognitionType.Achievement;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly Date { get; set; }
    public int CreatedById { get; set; }
}
