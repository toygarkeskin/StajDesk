using StajDesk.Domain.Common;
using StajDesk.Domain.Enums;

namespace StajDesk.Domain.Entities;

/// <summary>
/// Departman (organizasyon birimi). Üst departman ile ağaç oluşturur: Genel Müdürlük → BT Direktörlüğü → Destek Birimi.
/// Departman yöneticisi onay zincirlerinde "departman yöneticisi" adımının kime düşeceğini belirler.
/// </summary>
public class Department : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public int? ParentId { get; set; }
    public Department? Parent { get; set; }
    public ICollection<Department> Children { get; set; } = new List<Department>();

    public int? ManagerId { get; set; }
    public User? Manager { get; set; }

    public ICollection<User> Members { get; set; } = new List<User>();
}

/// <summary>
/// Unvan / pozisyon (CEO, Müdür, Uzman, Stajyer…). Seviye küçüldükçe kıdem artar (1 = en üst).
/// Unvan sistem rolü DEĞİLDİR: "Müdür" unvanlı biri sistemde yalnızca "Kullanıcı" rolünde olabilir; yetkiyi rol belirler.
/// </summary>
public class Position : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Personel kartı (kullanıcıyla 1:1). Kimlik/hesap bilgisi <see cref="User"/>'da, personel bilgisi burada.
/// Kişisel alanları (doğum tarihi, adres, acil durum kişisi) yalnızca kişinin kendisi ve yetkili İK görür.
/// </summary>
public class EmployeeProfile
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string? EmployeeNumber { get; set; }
    public DateOnly? HireDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

    public DateOnly? BirthDate { get; set; }
    public string? PersonalEmail { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }

    /// <summary>Profil ve kapak fotoğrafının depolama anahtarı (rastgele; dosya adı kullanılmaz).</summary>
    public string? PhotoKey { get; set; }
    public string? CoverKey { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Kişisel tercihler: dil, tema, e-posta bildirimleri.</summary>
public class UserSettings
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Language { get; set; } = "tr";
    public string Theme { get; set; } = "system";
    public bool EmailTicketUpdates { get; set; } = true;
    public bool EmailAnnouncements { get; set; } = true;
}

/// <summary>SMS ile tek kullanımlık kod (OTP). Kod yalnızca özet (hash) olarak saklanır; 5 hatalı denemede geçersiz olur.</summary>
public class OtpChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public OtpPurpose Purpose { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    /// <summary>Giriş için: OAuth yetkilendirme isteğinin parametreleri (JSON). Kod doğrulanınca yetki kodu bunlarla üretilir.</summary>
    public string? Payload { get; set; }
    public int Attempts { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
