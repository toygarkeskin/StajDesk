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

    public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default) =>
        GetOrSetAsync(key, factory, _ => ttl, ct);

    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, Func<T, TimeSpan> ttl, CancellationToken ct = default)
    {
        var fullKey = KeyPrefix + key;
        try
        {
            var cached = await redis.GetDatabase().StringGetAsync(fullKey);
            if (cached.HasValue) return JsonSerializer.Deserialize<T>(cached.ToString(), Json)!;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Redis okunamadı ({Key}); veritabanına gidiliyor: {Message}", key, ex.Message);
            return await factory(ct);
        }

        var value = await factory(ct);
        try
        {
            await redis.GetDatabase().StringSetAsync(fullKey, JsonSerializer.Serialize(value, Json), ttl(value));
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

public sealed class RateLimitOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int WindowSeconds { get; set; } = 300;
}

/// <summary>
/// Redis INCR + EXPIRE ile sabit pencereli sayaç: aynı anahtar için pencere içinde en fazla N deneme.
/// Kullanım: e-posta ve IP başına başarısız giriş, şifre sıfırlama isteği.
/// Redis kapalıysa kullanıcılar engellenmez (fail-open) ama durum loglanır.
/// </summary>
internal sealed class RedisRateLimiter(IConnectionMultiplexer redis, IOptions<RateLimitOptions> options, ILogger<RedisRateLimiter> logger) : IRateLimiter
{
    private static string Key(string key) => $"stajdesk:rl:{key}";

    public async Task<int?> GetBlockedSecondsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var count = (int?)await db.StringGetAsync(Key(key)) ?? 0;
            if (count < options.Value.MaxFailedAttempts) return null;
            var ttl = await db.KeyTimeToLiveAsync(Key(key));
            return (int)Math.Ceiling((ttl ?? TimeSpan.FromSeconds(options.Value.WindowSeconds)).TotalSeconds);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Hız sınırı kontrol edilemedi (Redis erişilemiyor): {Message}", ex.Message);
            return null;
        }
    }

    public async Task RegisterHitAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var count = await db.StringIncrementAsync(Key(key));
            if (count == 1) await db.KeyExpireAsync(Key(key), TimeSpan.FromSeconds(options.Value.WindowSeconds));
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Hız sınırı sayacı artırılamadı: {Message}", ex.Message);
        }
    }

    public async Task ResetAsync(string key, CancellationToken ct = default)
    {
        try { await redis.GetDatabase().KeyDeleteAsync(Key(key)); }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Hız sınırı sayacı sıfırlanamadı: {Message}", ex.Message);
        }
    }
}
