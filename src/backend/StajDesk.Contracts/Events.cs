namespace StajDesk.Contracts;

/// <summary>Kuyruk / exchange / routing key isimleri tek yerde.</summary>
public static class Messaging
{
    public const string Exchange = "stajdesk.events";
    public const string NotificationQueue = "stajdesk.notifications";
    public const string DeadLetterExchange = "stajdesk.events.dlx";
    public const string DeadLetterQueue = "stajdesk.notifications.dlq";

    public static class RoutingKeys
    {
        public const string TicketCreated = "ticket.created";
        public const string TicketStatusChanged = "ticket.status-changed";
        public const string TicketAssigned = "ticket.assigned";
        public const string TicketCommentAdded = "ticket.comment-added";
        public const string AllTicketEvents = "ticket.*";
        public const string UserRegistered = "user.registered";
        public const string PasswordResetRequested = "user.password-reset-requested";
        public const string AllUserEvents = "user.*";
    }
}

/// <summary>Tüm olayların ortak alanları.</summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record TicketCreatedEvent(
    int TicketId,
    string Title,
    string Priority,
    string CategoryName,
    int RequesterId,
    string RequesterName,
    int? StoreId = null) : IntegrationEvent;

public sealed record TicketStatusChangedEvent(
    int TicketId,
    string Title,
    string OldStatus,
    string NewStatus,
    int RequesterId,
    int? AssigneeId,
    int ChangedById,
    string ChangedByName) : IntegrationEvent;

public sealed record TicketAssignedEvent(
    int TicketId,
    string Title,
    int? AssigneeId,
    string? AssigneeName,
    int RequesterId,
    int AssignedById) : IntegrationEvent;

public sealed record TicketCommentAddedEvent(
    int TicketId,
    string Title,
    int CommentId,
    int AuthorId,
    string AuthorName,
    bool AuthorIsStaff,
    bool IsInternal,
    int RequesterId,
    int? AssigneeId) : IntegrationEvent;

public sealed record UserRegisteredEvent(int UserId, string Email, string FullName) : IntegrationEvent;

/// <summary>Şifre sıfırlama bağlantısı içerir; Worker e-posta olarak gönderir (demo ortamında Mailpit'e düşer).</summary>
public sealed record PasswordResetRequestedEvent(int UserId, string Email, string FullName, string ResetUrl, int ExpiresInMinutes) : IntegrationEvent;
