using StajDesk.Domain.Common;
using StajDesk.Domain.Enums;

namespace StajDesk.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Şifre asla düz metin saklanmaz; yalnızca hash (salt dahil) tutulur.</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<Ticket> RequestedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public bool IsStaff => Role is UserRole.Agent or UserRole.Admin;
}
