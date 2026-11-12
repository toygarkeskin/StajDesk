using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using StajDesk.Infrastructure.Realtime;

namespace StajDesk.Api.Infrastructure;

/// <summary>
/// Tarayıcıya anlık bildirim kanalı (SignalR). İstemci yalnızca dinler; sunucuya mesaj gönderemez.
/// Her bağlantı JWT'deki "sub" ile eşleşir; mesaj yalnızca ilgili kullanıcının sekmelerine gider.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub;

/// <summary>SignalR'ın "kullanıcı" kavramı = JWT "sub" (kullanıcı kimliği).</summary>
public sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}

/// <summary>Redis kanalını (Worker'ın yayınladığı) dinler ve SignalR ile ilgili kullanıcılara iletir.</summary>
public sealed class RealtimeBridge(IConnectionMultiplexer redis, IHubContext<NotificationHub> hub, ILogger<RealtimeBridge> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var sub = redis.GetSubscriber();
                await sub.SubscribeAsync(RealtimeChannel.Name, (channel, value) => { _ = ForwardAsync(value); });
                logger.LogInformation("Gerçek zamanlı bildirim kanalı dinleniyor.");
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Gerçek zamanlı kanala abone olunamadı, 10 sn sonra tekrar denenecek: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ForwardAsync(RedisValue value)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<RealtimeMessage>(value.ToString(), RealtimeChannel.Json);
            if (msg is null || msg.UserIds.Length == 0) return;
            await hub.Clients.Users(msg.UserIds.Select(id => id.ToString())).SendAsync(msg.Kind, new { msg.TicketId, msg.Title });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gerçek zamanlı bildirim iletilemedi.");
        }
    }
}
