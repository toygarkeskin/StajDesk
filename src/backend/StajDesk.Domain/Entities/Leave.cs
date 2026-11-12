using StajDesk.Domain.Common;

namespace StajDesk.Domain.Entities;

/// <summary>İzin türü (yönetilebilir): yıllık, mazeret, ücretsiz, doğum, süt, görev, uzaktan çalışma, denkleştirme...</summary>
public class LeaveType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Bakiye takibi yapılır mı (yıllık, mazeret). Ücretsiz izin gibi türlerde false.</summary>
    public bool TracksBalance { get; set; }
    /// <summary>Yıllık varsayılan hak (gün). Kişiye özel hak tanımlanmadıysa bu kullanılır.</summary>
    public decimal DefaultDaysPerYear { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>Kişinin bir yıl için izin hakkı (İzin yöneticisi tanımlar; yoksa türün varsayılanı geçerlidir).</summary>
public class LeaveEntitlement
{
    public int UserId { get; set; }
    public int LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal Days { get; set; }
    public string? Note { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum LeaveStatus { Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4 }

public class LeaveRequest : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    /// <summary>İş günü sayısı (hafta sonu ve sabit tarihli resmî tatiller hariç)</summary>
    public decimal Days { get; set; }
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public int? WorkflowInstanceId { get; set; }
}
