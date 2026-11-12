using StajDesk.Domain.Common;

namespace StajDesk.Domain.Entities;

/* =========================================================
   Yapılandırılabilir onay (iş akışı) motoru.
   Tanım: bir talep türü için sıralı onay adımları (ör. izin: yönetici → İzin yöneticisi).
   Örnek: her talep için bir akış örneği ve adım başına bir onay kaydı.
   Onaylayıcı adımın tipine göre talep sahibinden çözülür (yöneticisi, departman yöneticisi, belirli kişi, rol).
   ========================================================= */

public enum ApproverType
{
    /// <summary>Talep sahibinin doğrudan yöneticisi</summary>
    Manager = 1,
    /// <summary>Yöneticinin yöneticisi (ör. müdür onayı)</summary>
    SecondLevelManager = 2,
    /// <summary>Talep sahibinin departmanının (veya adımda seçilen departmanın) yöneticisi</summary>
    DepartmentManager = 3,
    /// <summary>Belirli bir kişi</summary>
    User = 4,
    /// <summary>Bu role sahip herhangi biri</summary>
    Role = 5,
}

public enum WorkflowStatus { Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4 }
public enum ApprovalDecision { Pending = 1, Approved = 2, Rejected = 3, Skipped = 4 }

public class WorkflowDefinition : BaseEntity
{
    /// <summary>Talep türü anahtarı: "leave", "expense"... Kodun bağlandığı sabit ad.</summary>
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<WorkflowStepDefinition> Steps { get; set; } = new List<WorkflowStepDefinition>();
}

public class WorkflowStepDefinition
{
    public int Id { get; set; }
    public int WorkflowDefinitionId { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public ApproverType ApproverType { get; set; }
    /// <summary>ApproverType=Role için rol adı</summary>
    public string? RoleName { get; set; }
    /// <summary>ApproverType=User için kişi</summary>
    public int? UserId { get; set; }
    /// <summary>ApproverType=DepartmentManager için sabit departman (boşsa talep sahibinin departmanı) — departman bazlı yönlendirme</summary>
    public int? DepartmentId { get; set; }
}

public class WorkflowInstance : BaseEntity
{
    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition Definition { get; set; } = null!;
    /// <summary>Bağlı kayıt: "leave_request" + kimlik</summary>
    public string SubjectType { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int RequesterId { get; set; }
    public User Requester { get; set; } = null!;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
    public int CurrentStep { get; set; } = 1;
    public DateTime? CompletedAt { get; set; }
    public ICollection<WorkflowApproval> Approvals { get; set; } = new List<WorkflowApproval>();
}

public class WorkflowApproval
{
    public int Id { get; set; }
    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance Instance { get; set; } = null!;
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    /// <summary>Çözülmüş onaylayıcı (kişi). Rol adımlarında boştur; rolün herhangi bir üyesi karar verebilir.</summary>
    public int? ApproverUserId { get; set; }
    public User? ApproverUser { get; set; }
    public string? ApproverRole { get; set; }
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public int? DecidedById { get; set; }
    public User? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Comment { get; set; }
}
