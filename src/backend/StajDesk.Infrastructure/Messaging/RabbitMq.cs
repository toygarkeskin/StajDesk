using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StajDesk.Infrastructure.Persistence;
using Msg = StajDesk.Contracts.Messaging;

namespace StajDesk.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
}

public static class RabbitMqTopology
{
    public static ConnectionFactory CreateFactory(RabbitMqOptions o, string clientName) => new()
    {
        HostName = o.Host,
        Port = o.Port,
        UserName = o.UserName,
        Password = o.Password,
        VirtualHost = o.VirtualHost,
        ClientProvidedName = clientName,
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
    };

    /// <summary>
    /// Exchange, kuyruk ve bağları tanımlar. İdempotenttir: API de Worker da çağırabilir.
    /// durable = true → RabbitMQ yeniden başlasa da kuyruk ve kalıcı mesajlar kaybolmaz.
    /// </summary>
    public static async Task DeclareAsync(IChannel channel, CancellationToken ct = default)
    {
        await channel.ExchangeDeclareAsync(Msg.DeadLetterExchange, ExchangeType.Fanout, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(Msg.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(Msg.DeadLetterQueue, Msg.DeadLetterExchange, routingKey: "", cancellationToken: ct);

        await channel.ExchangeDeclareAsync(Msg.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(Msg.NotificationQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = Msg.DeadLetterExchange },
            cancellationToken: ct);
        await channel.QueueBindAsync(Msg.NotificationQueue, Msg.Exchange, Msg.RoutingKeys.AllTicketEvents, cancellationToken: ct);
        await channel.QueueBindAsync(Msg.NotificationQueue, Msg.Exchange, Msg.RoutingKeys.AllUserEvents, cancellationToken: ct);
    }
}

/// <summary>
/// Transactional outbox dağıtıcısı (arka plan servisi):
/// yayınlanmamış olayları sırayla okur, publisher-confirm ile RabbitMQ'ya iletir ve "yayınlandı" işaretler.
/// Broker kapalıyken olaylar veritabanında güvenle bekler; broker geri gelince gönderilir (en az bir kez teslim).
/// Birden fazla API örneği çalışırsa FOR UPDATE SKIP LOCKED sayesinde aynı mesaj iki kez alınmaz.
/// </summary>
public sealed class OutboxDispatcher(IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var sent = await DispatchBatchAsync(stoppingToken);
                if (sent == 0) await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Outbox dağıtımı başarısız; tekrar denenecek: {Message}", ex.Message);
                await ResetAsync();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(token);
            var batch = await db.OutboxMessages
                .FromSqlRaw("SELECT * FROM outbox_messages WHERE published_at IS NULL ORDER BY occurred_at LIMIT 50 FOR UPDATE SKIP LOCKED")
                .ToListAsync(token);
            if (batch.Count == 0) return 0;

            var channel = await GetChannelAsync(token);
            foreach (var msg in batch)
            {
                msg.Attempts++;
                var props = new BasicProperties
                {
                    Persistent = true, ContentType = "application/json", MessageId = msg.Id.ToString(), Type = msg.RoutingKey,
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                };
                await channel.BasicPublishAsync(Msg.Exchange, msg.RoutingKey, mandatory: false, basicProperties: props,
                    body: Encoding.UTF8.GetBytes(msg.Payload), cancellationToken: token);
                msg.PublishedAt = DateTime.UtcNow;
                logger.LogInformation("Olay yayınlandı: {RoutingKey} {EventId}", msg.RoutingKey, msg.Id);
            }
            await db.SaveChangesAsync(token);
            await tx.CommitAsync(token);
            return batch.Count;
        }, ct);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        await ResetAsync();
        var factory = RabbitMqTopology.CreateFactory(options.Value, "stajdesk-api");
        _connection = await factory.CreateConnectionAsync(ct);
        // Publisher confirm: broker mesajı diske aldığını onaylamadan "yayınlandı" saymayız.
        _channel = await _connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await RabbitMqTopology.DeclareAsync(_channel, ct);
        logger.LogInformation("Outbox dağıtıcısı RabbitMQ'ya bağlandı ({Host})", options.Value.Host);
        return _channel;
    }

    private async Task ResetAsync()
    {
        try { if (_channel is not null) await _channel.DisposeAsync(); } catch { /* yoksay */ }
        try { if (_connection is not null) await _connection.DisposeAsync(); } catch { /* yoksay */ }
        _channel = null;
        _connection = null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await ResetAsync();
    }
}
