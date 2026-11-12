using StajDesk.Domain.Common;

namespace StajDesk.Domain.Entities;

/// <summary>Worker servisinin RabbitMQ olaylarından ürettiği kullanıcı bildirimi.</summary>
public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int? TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>Tıklanınca gidilecek portal adresi (ör. /approvals). Talep bildirimlerinde TicketId kullanılır.</summary>
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
