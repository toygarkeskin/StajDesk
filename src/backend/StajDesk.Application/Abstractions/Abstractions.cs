using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Common;
using StajDesk.Contracts;
using StajDesk.Domain.Entities;

namespace StajDesk.Application.Abstractions;

/// <summary>Servislerin veritabanına eriştiği sözleşme. Gerçeği Infrastructure'daki AppDbContext'tir.</summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Store> Stores { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Category> Categories { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketComment> TicketComments { get; }
    DbSet<TicketHistory> TicketHistories { get; }
    DbSet<Asset> Assets { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<OAuthClient> OAuthClients { get; }
    DbSet<AuthorizationCode> AuthorizationCodes { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<SentEmail> SentEmails { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<OnCallShift> OnCallShifts { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }
    DbSet<Department> Departments { get; }
    DbSet<Position> Positions { get; }
    DbSet<EmployeeProfile> EmployeeProfiles { get; }
    DbSet<UserSettings> UserSettings { get; }
    DbSet<OtpChallenge> OtpChallenges { get; }
    DbSet<AccountRequest> AccountRequests { get; }
    DbSet<NewsArticle> News { get; }
    DbSet<Advertisement> Advertisements { get; }
    DbSet<AdStatDaily> AdStats { get; }
    DbSet<JobOpening> JobOpenings { get; }
    DbSet<SiteSetting> SiteSettings { get; }
    DbSet<MediaFile> MediaFiles { get; }
    DbSet<ExternalSnapshot> ExternalSnapshots { get; }
    DbSet<DashboardWidgetConfig> DashboardWidgets { get; }
    DbSet<UserDashboardPreference> DashboardPreferences { get; }
    DbSet<CompanyEvent> CompanyEvents { get; }
    DbSet<Recognition> Recognitions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Kaydedilmemiş değişiklikleri atar. Toplu işlemlerde başarısız olan bir öğenin yarım değişikliği sonrakilere taşınmasın diye.</summary>
    void DiscardChanges()
    {
        if (this is DbContext context) context.ChangeTracker.Clear();
    }
}

/// <summary>
/// İsteği yapan taraf (JWT'den + veritabanındaki rol/izinlerden).
/// İş kuralları rol adına değil izne bakar: <c>currentUser.Require(Permissions.TicketsManage)</c>.
/// </summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string? FullName { get; }
    int? StoreId { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlySet<string> Permissions { get; }
    /// <summary>client_credentials ile gelen servis istemcisi (kullanıcısız token).</summary>
    string? ClientId { get; }
    string? IpAddress { get; }

    bool IsAuthenticated => UserId.HasValue || ClientId is not null;
    bool Has(string permission) => Permissions.Contains(permission);
    bool HasAny(params string[] permissions) => permissions.Any(Permissions.Contains);

    int RequireUserId() => UserId ?? throw new UnauthorizedException("Oturum bulunamadı veya süresi doldu. Devam etmek için giriş yapın.");

    void Require(params string[] anyOf)
    {
        if (!IsAuthenticated) throw new UnauthorizedException("Oturum bulunamadı veya süresi doldu. Devam etmek için giriş yapın.");
        if (!HasAny(anyOf)) throw new ForbiddenException();
    }

    /// <summary>Destek ekibi mi? (Talepleri yönetebilen herkes.)</summary>
    bool IsStaff => Has(Domain.Security.Permissions.TicketsManage);
}

/// <summary>Redis üzerinde cache-aside deseni. Redis erişilemezse doğrudan factory çalışır.</summary>
public interface ICacheService
{
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default);
    /// <summary>Süre sonuca göre seçilir (ör. dış kaynak hatasında kısa süre, başarıda uzun süre).</summary>
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, Func<T, TimeSpan> ttl, CancellationToken ct = default) =>
        GetOrSetAsync(key, factory, TimeSpan.FromMinutes(1), ct);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

/// <summary>Sayaç tabanlı hız sınırlama (Redis INCR + EXPIRE).</summary>
public interface IRateLimiter
{
    /// <returns>Engelliyse kalan süre (saniye), değilse null.</returns>
    Task<int?> GetBlockedSecondsAsync(string key, CancellationToken ct = default);
    Task RegisterHitAsync(string key, CancellationToken ct = default);
    Task ResetAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Olayları transactional outbox'a ekler. Olay, iş verisiyle aynı SaveChanges çağrısında kaydedilir;
/// arka plandaki dağıtıcı RabbitMQ'ya iletir.
/// </summary>
public interface IEventPublisher
{
    void Enqueue<T>(T @event, string routingKey) where T : IntegrationEvent;
}

/// <summary>Birden fazla SaveChanges'i tek transaction'da çalıştırır (ya hepsi ya hiçbiri).</summary>
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface IAccessTokenIssuer
{
    /// <summary>RS256 imzalı JWT erişim token'ı üretir.</summary>
    string Issue(AccessTokenClaims claims);
    int ExpiresInSeconds { get; }
}

public sealed record AccessTokenClaims(int? UserId, string ClientId, string Scope, IReadOnlyCollection<string> Roles, string? Name, Guid? SessionId);

public interface IAuditLogger
{
    void Add(string action, string entity, object? entityId, object? data = null);
}

public static class CacheKeys
{
    public const string Categories = "categories:all";
    public const string HomePrefix = "home:";
    public const string SitePrefix = "site:";
    public const string StatsPrefix = "stats:";
    public static string Permissions(int userId) => $"perms:user:{userId}";
    public const string PermissionsPrefix = "perms:";
}

/// <summary>
/// Talep araması. Varsayılan uygulama basit "içerir" araması yapar (testler, bellek içi veritabanı);
/// Infrastructure PostgreSQL tam metin aramasıyla değiştirir.
/// </summary>
public interface ITicketSearch
{
    IQueryable<Ticket> Apply(IQueryable<Ticket> query, string term);
}

internal sealed class ContainsTicketSearch : ITicketSearch
{
    public IQueryable<Ticket> Apply(IQueryable<Ticket> query, string term)
    {
        var s = term.Trim().ToLower();
        if (s.Length == 0) return query;
        return int.TryParse(s.TrimStart('#'), out var id)
            ? query.Where(t => t.Id == id || t.Title.ToLower().Contains(s))
            : query.Where(t => t.Title.ToLower().Contains(s) || t.Description.ToLower().Contains(s));
    }
}

/// <summary>Talep eklerinin saklandığı yer (yerelde disk / Docker volume). Anahtar her zaman sunucu tarafında üretilir.</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// SMS gönderimi (OTP). Gerçek sağlayıcı (Netgsm, İleti Merkezi…) yapılandırılmadıysa <see cref="IsConfigured"/> false döner
/// ve SMS adımı kapalı kalır — sistem SMS gönderilmiş gibi davranmaz.
/// </summary>
public interface ISmsSender
{
    bool IsConfigured { get; }
    string ProviderName { get; }
    Task SendAsync(string phone, string message, CancellationToken ct = default);
}
