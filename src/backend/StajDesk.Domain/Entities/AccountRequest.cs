using StajDesk.Domain.Common;
using StajDesk.Domain.Enums;

namespace StajDesk.Domain.Entities;

/// <summary>
/// Kurumsal hesap açma / kapatma talebi.
///  - İnsan Kaynakları talebi açar (accounts.request): yeni çalışanın personel bilgileri veya ayrılan çalışanın kapanış tarihi.
///  - Bilgi Teknolojileri / yönetici inceler (users.manage): onaylar (hesabı açar ya da kapatır) veya gerekçeyle reddeder.
/// Böylece İK'nın personel yetkisi kendiliğinden hesap açma / yetki verme yetkisine dönüşmez.
/// </summary>
public class AccountRequest : BaseEntity
{
    public AccountRequestType Type { get; set; }
    public AccountRequestStatus Status { get; set; } = AccountRequestStatus.Pending;

    // ---- Yeni hesap: personel bilgileri ----
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? EmployeeNumber { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? PositionId { get; set; }
    public Position? Position { get; set; }
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
    public int? StoreId { get; set; }
    public Store? Store { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public DateOnly? HireDate { get; set; }
    public string? Phone { get; set; }

    // ---- Hesap kapatma: ayrılan çalışan ----
    public int? TargetUserId { get; set; }
    public User? TargetUser { get; set; }
    public DateOnly? TerminationDate { get; set; }

    public string? Note { get; set; }

    public int RequestedById { get; set; }
    public User RequestedBy { get; set; } = null!;

    public int? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    /// <summary>Onaylanan yeni hesap talebiyle açılan kullanıcı.</summary>
    public int? CreatedUserId { get; set; }
}
