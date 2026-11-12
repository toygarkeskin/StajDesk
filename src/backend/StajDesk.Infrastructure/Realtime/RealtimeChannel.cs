using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace StajDesk.Infrastructure.Realtime;

/// <summary>
/// Gerçek zamanlı bildirim köprüsü: Worker bildirimi veritabanına yazınca bu kanala kısa bir mesaj bırakır,
/// API kanalı dinler ve SignalR ile ilgili kullanıcıların tarayıcısına iletir.
/// Mesajda yalnızca kimlikler ve başlık var; asıl veri her zaman yetki kontrollü API'den okunur.
/// </summary>
public sealed record RealtimeMessage(int[] UserIds, string Kind, int? TicketId, string? Title);

public static class RealtimeChannel
{
    public static readonly RedisChannel Name = RedisChannel.Literal("stajdesk:realtime");
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}

public sealed class RealtimePublisher(IConnectionMultiplexer redis, ILogger<RealtimePublisher> logger)
{
    /// <summary>Fail-open: Redis yoksa yalnızca uyarı loglanır; kullanıcılar bildirimi bir sonraki yenilemede görür.</summary>
    public async Task PublishAsync(RealtimeMessage message)
    {
        if (message.UserIds.Length == 0) return;
        try
        {
            await redis.GetSubscriber().PublishAsync(RealtimeChannel.Name, JsonSerializer.Serialize(message, RealtimeChannel.Json));
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Gerçek zamanlı bildirim yayınlanamadı (Redis erişilemiyor): {Message}", ex.Message);
        }
    }
}

/// <summary>Uygulama katmanının anlık sinyal arayüzü: aynı Redis kanalı → API → SignalR yolunu kullanır.</summary>
public sealed class RealtimeNotifier(RealtimePublisher publisher) : Application.Features.Social.IRealtimeNotifier
{
    public Task NotifyAsync(IReadOnlyCollection<int> userIds, string kind, string? title, CancellationToken ct = default) =>
        publisher.PublishAsync(new RealtimeMessage(userIds.ToArray(), kind, null, title));
}
