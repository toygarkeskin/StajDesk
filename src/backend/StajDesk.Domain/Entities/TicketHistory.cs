using StajDesk.Domain.Common;
using StajDesk.Domain.Enums;

namespace StajDesk.Domain.Entities;

/// <summary>Talep üzerindeki her önemli değişikliğin kaydı (zaman çizelgesi için).</summary>
public class TicketHistory : BaseEntity
{
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public int? ActorId { get; set; }
    public User? Actor { get; set; }

    public TicketHistoryAction Action { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
