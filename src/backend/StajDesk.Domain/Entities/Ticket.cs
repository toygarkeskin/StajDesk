using StajDesk.Domain.Common;
using StajDesk.Domain.Enums;

namespace StajDesk.Domain.Entities;

public class Ticket : BaseEntity
{
    /// <summary>Hangi durumdan hangi duruma geçilebilir? İş kuralı burada, tek yerde.</summary>
    private static readonly Dictionary<TicketStatus, TicketStatus[]> AllowedTransitions = new()
    {
        [TicketStatus.Open] = [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.InProgress] = [TicketStatus.Open, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Resolved] = [TicketStatus.InProgress, TicketStatus.Closed],
        [TicketStatus.Closed] = [TicketStatus.Open]
    };

    private Ticket() { } // EF Core için

    public Ticket(string title, string description, int requesterId, int categoryId,
        TicketPriority priority = TicketPriority.Medium, TicketType type = TicketType.Incident, int? assetId = null)
    {
        SetTitle(title);
        SetDescription(description);
        RequesterId = requesterId;
        CategoryId = categoryId;
        Priority = priority;
        Type = type;
        AssetId = assetId;
        Status = TicketStatus.Open;
    }

    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketType Type { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    public int RequesterId { get; private set; }
    public User Requester { get; private set; } = null!;

    public int? AssigneeId { get; private set; }
    public User? Assignee { get; private set; }

    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public int? AssetId { get; private set; }
    public Asset? Asset { get; private set; }

    public ICollection<TicketComment> Comments { get; private set; } = new List<TicketComment>();
    public ICollection<TicketHistory> History { get; private set; } = new List<TicketHistory>();

    public bool IsEditable => Status is TicketStatus.Open or TicketStatus.InProgress;

    public void SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Talep başlığı boş olamaz.");
        if (title.Trim().Length > 200)
            throw new DomainException("Talep başlığı en fazla 200 karakter olabilir.");
        Title = title.Trim();
    }

    public void SetDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Talep açıklaması boş olamaz.");
        Description = description.Trim();
    }

    public void UpdateDetails(string title, string description, int categoryId, TicketType type, int? assetId)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException("Kapanmış bir talep düzenlenemez.");
        SetTitle(title);
        SetDescription(description);
        CategoryId = categoryId;
        Type = type;
        AssetId = assetId;
        Touch();
    }

    public static bool CanTransition(TicketStatus from, TicketStatus to) =>
        AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public void ChangeStatus(TicketStatus newStatus)
    {
        if (newStatus == Status)
            throw new DomainException("Talep zaten bu durumda.");
        if (!CanTransition(Status, newStatus))
            throw new DomainException($"'{Status}' durumundan '{newStatus}' durumuna geçilemez.");

        Status = newStatus;
        var now = DateTime.UtcNow;
        ResolvedAt = newStatus == TicketStatus.Resolved ? now : newStatus == TicketStatus.Closed ? ResolvedAt ?? now : null;
        ClosedAt = newStatus == TicketStatus.Closed ? now : null;
        Touch();
    }

    public void ChangePriority(TicketPriority priority)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException("Kapanmış bir talebin önceliği değiştirilemez.");
        Priority = priority;
        Touch();
    }

    public void AssignTo(int? agentId)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException("Kapanmış bir talep atanamaz.");
        AssigneeId = agentId;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
