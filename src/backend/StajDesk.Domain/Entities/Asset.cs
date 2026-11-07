using StajDesk.Domain.Common;
using StajDesk.Domain.Enums;

namespace StajDesk.Domain.Entities;

/// <summary>Envanter kaydı (GLPI'daki "Assets" modülü): bilgisayar, yazıcı, telefon vb.</summary>
public class Asset : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public AssetType Type { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.InStock;
    public string? SerialNumber { get; set; }
    public string InventoryNumber { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Location { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyEndDate { get; set; }
    public string? Notes { get; set; }

    public int? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
