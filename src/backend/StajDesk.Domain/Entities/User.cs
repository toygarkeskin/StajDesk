using StajDesk.Domain.Common;

namespace StajDesk.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Şifre asla düz metin saklanmaz; yalnızca hash (salt dahil) tutulur.</summary>
    public string PasswordHash { get; set; } = string.Empty;
    /// <summary>E-posta yerine girişte kullanılabilir (benzersiz, küçük harf).</summary>
    public string? Username { get; set; }
    /// <summary>Kendi kaydolan kullanıcı e-postasını doğrulayana kadar giriş yapamaz. Davetle gelenler şifre belirleyince doğrulanır.</summary>
    public DateTime? EmailVerifiedAt { get; set; }
    /// <summary>Girişte SMS ile ikinci doğrulama (yalnızca SMS sağlayıcısı yapılandırılmışsa açılabilir).</summary>
    public bool TwoFactorEnabled { get; set; }
    /// <summary>BT tarafından kilitlendiyse dolu: şifre doğru olsa bile giriş yapılamaz, açık oturumlar kapatılır.</summary>
    public DateTime? LockedAt { get; set; }
    public string? LockReason { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? PositionId { get; set; }
    public Position? Position { get; set; }
    /// <summary>Bağlı olduğu yönetici (organizasyon şeması ve onay zincirleri).</summary>
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
    public ICollection<User> DirectReports { get; set; } = new List<User>();
    public EmployeeProfile? Profile { get; set; }
    public UserSettings? Settings { get; set; }

    public int? StoreId { get; set; }
    public Store? Store { get; set; }

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    public ICollection<Ticket> RequestedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
