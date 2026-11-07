using Microsoft.EntityFrameworkCore;
using StajDesk.Contracts;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.Application.Abstractions;

/// <summary>Servislerin veritabanına eriştiği sözleşme. Gerçeği Infrastructure'daki AppDbContext'tir.</summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketComment> TicketComments { get; }
    DbSet<TicketHistory> TicketHistories { get; }
    DbSet<Asset> Assets { get; }
    DbSet<Notification> Notifications { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>İsteği yapan kullanıcının kimliği (JWT'den okunur).</summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string? FullName { get; }
    UserRole? Role { get; }
    bool IsAuthenticated => UserId.HasValue;
    bool IsStaff => Role is UserRole.Agent or UserRole.Admin;
    bool IsAdmin => Role == UserRole.Admin;
    int RequireUserId() => UserId ?? throw new Common.UnauthorizedException("Oturum bulunamadı veya süresi doldu. Devam etmek için giriş yapın.");
}

/// <summary>Redis üzerinde cache-aside deseni. Redis erişilemezse doğrudan factory çalışır.</summary>
public interface ICacheService
{
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

/// <summary>Brute-force koruması: e-posta başına başarısız giriş sayacı.</summary>
public interface ILoginRateLimiter
{
    /// <returns>Engelliyse kalan süre (saniye), değilse null.</returns>
    Task<int?> GetBlockedSecondsAsync(string email, CancellationToken ct = default);
    Task RegisterFailureAsync(string email, CancellationToken ct = default);
    Task ResetAsync(string email, CancellationToken ct = default);
}

/// <summary>Olayları mesaj kuyruğuna (RabbitMQ) bırakır.</summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, string routingKey, CancellationToken ct = default) where T : IntegrationEvent;
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}

public static class CacheKeys
{
    public const string Categories = "categories:all";
    public const string StatsPrefix = "stats:";
    public static string Stats(int? userId) => userId is null ? "stats:staff" : $"stats:user:{userId}";
}
