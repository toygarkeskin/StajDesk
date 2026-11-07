using StajDesk.Domain.Common;

namespace StajDesk.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Arayüzde rozet rengi (ör. "violet", "sky", "emerald").</summary>
    public string Color { get; set; } = "violet";
    public bool IsActive { get; set; } = true;

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
