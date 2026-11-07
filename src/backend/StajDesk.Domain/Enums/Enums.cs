namespace StajDesk.Domain.Enums;

public enum UserRole
{
    User = 0,
    Agent = 1,
    Admin = 2
}

/// <summary>Talep yaşam döngüsü: Açık → İşlemde → Çözüldü → Kapandı</summary>
public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3
}

public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>GLPI'daki gibi: Olay (bir şey bozuldu) veya İstek (bir şey isteniyor).</summary>
public enum TicketType
{
    Incident = 0,
    Request = 1
}

public enum AssetType
{
    Computer = 0,
    Laptop = 1,
    Monitor = 2,
    Printer = 3,
    Phone = 4,
    NetworkDevice = 5,
    Software = 6,
    Other = 7
}

public enum AssetStatus
{
    InUse = 0,
    InStock = 1,
    InRepair = 2,
    Retired = 3
}

public enum TicketHistoryAction
{
    Created = 0,
    StatusChanged = 1,
    Assigned = 2,
    PriorityChanged = 3,
    Updated = 4
}
