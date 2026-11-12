namespace StajDesk.Domain.Enums;

/// <summary>Talep yaşam döngüsü: Açık → İşlemde ⇄ Beklemede → Çözüldü → Kapandı</summary>
public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3,
    /// <summary>Kullanıcıdan bilgi veya parça bekleniyor.</summary>
    Pending = 4
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
    Other = 7,
    PointOfSale = 8
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
    Updated = 4,
    AttachmentAdded = 5,
    AttachmentRemoved = 6
}

/// <summary>Duyurunun önemi: arayüzde renk ve sıralama için.</summary>
public enum AnnouncementLevel
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum EmploymentType
{
    FullTime = 0,
    PartTime = 1,
    Contract = 2,
    Intern = 3
}

/// <summary>Hesap bağlantılarının amacı (şifre sıfırlama, davet ile ilk şifre, e-posta doğrulama).</summary>
public enum AccountTokenPurpose
{
    PasswordReset = 0,
    Invitation = 1,
    EmailVerification = 2
}

public enum OtpPurpose
{
    Login = 0,
    EnableTwoFactor = 1
}

public enum AccountRequestType
{
    /// <summary>Yeni çalışan için portal hesabı</summary>
    NewAccount = 0,
    /// <summary>Ayrılan çalışanın hesabının kapatılması</summary>
    Deactivation = 1
}

public enum AccountRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}
