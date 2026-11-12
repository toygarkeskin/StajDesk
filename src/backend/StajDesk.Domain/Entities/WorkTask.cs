using StajDesk.Domain.Common;

namespace StajDesk.Domain.Entities;

public enum WorkTaskStatus { Todo = 1, InProgress = 2, Done = 3, Cancelled = 4 }
public enum WorkTaskPriority { Low = 1, Normal = 2, High = 3 }

/// <summary>
/// Görev (iş takibi): bir çalışana atanan yapılacak iş. Destek talebinden farklıdır — talep "sorun bildirimi",
/// görev "yönetici/ekip içi iş ataması"dır. Kişi kendine görev açabilir; başkasına yalnızca kendi ekibine (organizasyonda altındakiler)
/// veya tasks.manage yetkisiyle atayabilir.
/// </summary>
public class WorkTask : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int AssigneeId { get; set; }
    public User Assignee { get; set; } = null!;
    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public DateOnly? DueDate { get; set; }
    public WorkTaskPriority Priority { get; set; } = WorkTaskPriority.Normal;
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Todo;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>Duyuru okundu bilgisi.</summary>
public class AnnouncementRead
{
    public int AnnouncementId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
