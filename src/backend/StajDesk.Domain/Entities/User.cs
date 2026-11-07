using StajDesk.Domain.Common;

namespace StajDesk.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Şifre asla düz metin saklanmaz; yalnızca hash (salt dahil) tutulur.</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public int? StoreId { get; set; }
    public Store? Store { get; set; }

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    public ICollection<Ticket> RequestedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
