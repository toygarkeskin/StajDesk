using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StajDesk.Application.Abstractions;

namespace StajDesk.Infrastructure.Caching;

/// <summary>
/// Cache-aside uygulaması. Redis kapalıysa uygulama durmaz; uyarı loglanır ve veri doğrudan veritabanından gelir.
/// </summary>
internal sealed class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
    private const string KeyPrefix = "stajdesk:";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default)
    {
        var fullKey = KeyPrefix + key;
        try
        {
            var cached = await redis.GetDatabase().StringGetAsync(fullKey);
            if (cached.HasValue)
            {
                logger.LogDebug("Önbellekten okundu: {Key}", key);
                return JsonSerializer.Deserialize<T>(cached.ToString(), Json)!;
            }
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Redis okunamadı ({Key}); veritabanına gidiliyor: {Message}", key, ex.Message);
            return await factory(ct);
        }

        var value = await factory(ct);
        try
        {
            await redis.GetDatabase().StringSetAsync(fullKey, JsonSerializer.Serialize(value, Json), ttl);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Redis'e yazılamadı ({Key}): {Message}", key, ex.Message);
        }
        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try { await redis.GetDatabase().KeyDeleteAsync(KeyPrefix + key); }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Redis anahtarı silinemedi ({Key}): {Message}", key, ex.Message);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            foreach (var endpoint in redis.GetEndPoints())
            {
                var server = redis.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica) continue;
                await foreach (var key in server.KeysAsync(pattern: $"{KeyPrefix}{prefix}*"))
                    await db.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Redis önbelleği temizlenemedi ({Prefix}): {Message}", prefix, ex.Message);
        }
    }
}

public sealed class LoginRateLimitOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int WindowSeconds { get; set; } = 300;
}

/// <summary>
/// Redis INCR + EXPIRE ile sayaç tabanlı hız sınırlama: aynı e-posta ile 5 dakikada en fazla 5 hatalı giriş.
/// </summary>
internal sealed class RedisLoginRateLimiter(
    IConnectionMultiplexer redis,
    IOptions<LoginRateLimitOptions> options,
    ILogger<RedisLoginRateLimiter> logger) : ILoginRateLimiter
{
    private static string Key(string email) => $"stajdesk:login-fail:{email}";

    public async Task<int?> GetBlockedSecondsAsync(string email, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var count = (int?)await db.StringGetAsync(Key(email)) ?? 0;
            if (count < options.Value.MaxFailedAttempts) return null;
            var ttl = await db.KeyTimeToLiveAsync(Key(email));
            return (int)Math.Ceiling((ttl ?? TimeSpan.FromSeconds(options.Value.WindowSeconds)).TotalSeconds);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // Bilinçli tercih: Redis kapalıyken kullanıcılar giriş yapabilsin (fail-open), ama loglansın.
            logger.LogWarning("Hız sınırlama kontrol edilemedi (Redis erişilemiyor): {Message}", ex.Message);
            return null;
        }
    }

    public async Task RegisterFailureAsync(string email, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var count = await db.StringIncrementAsync(Key(email));
            if (count == 1)
                await db.KeyExpireAsync(Key(email), TimeSpan.FromSeconds(options.Value.WindowSeconds));
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Başarısız giriş sayacı artırılamadı: {Message}", ex.Message);
        }
    }

    public async Task ResetAsync(string email, CancellationToken ct = default)
    {
        try { await redis.GetDatabase().KeyDeleteAsync(Key(email)); }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Giriş sayacı sıfırlanamadı: {Message}", ex.Message);
        }
    }
}
