using StajDesk.Domain.Common;

namespace StajDesk.Domain.Entities;

public class TicketComment : BaseEntity
{
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    /// <summary>Dahili not: yalnızca destek personeli görür (GLPI'daki "private followup").</summary>
    public bool IsInternal { get; set; }
}
