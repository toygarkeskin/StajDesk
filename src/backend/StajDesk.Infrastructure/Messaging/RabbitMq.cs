using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StajDesk.Application.Abstractions;
using StajDesk.Contracts;
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
        NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
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
    }
}

/// <summary>
/// Olayları RabbitMQ'ya yayınlar. Tek bağlantı + tek kanal (singleton), eşzamanlı yayınlar kilitle sıraya alınır.
/// Broker'a ulaşılamazsa istek başarısız olmaz; hata loglanır (iyileştirme fikri: Outbox deseni).
/// </summary>
internal sealed class RabbitMqEventPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync<T>(T @event, string routingKey, CancellationToken ct = default) where T : IntegrationEvent
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event, Json));
        var props = new BasicProperties
        {
            Persistent = true, // mesaj diske yazılır
            ContentType = "application/json",
            MessageId = @event.EventId.ToString(),
            Type = routingKey,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _lock.WaitAsync(ct);
        try
        {
            var channel = await GetChannelAsync(ct);
            await channel.BasicPublishAsync(Msg.Exchange, routingKey, mandatory: false, basicProperties: props, body: body, cancellationToken: ct);
            logger.LogInformation("Olay yayınlandı: {RoutingKey} {EventId}", routingKey, @event.EventId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Olay yayınlanamadı: {RoutingKey} {EventId}", routingKey, @event.EventId);
            await ResetAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        await ResetAsync();

        var factory = RabbitMqTopology.CreateFactory(options.Value, "stajdesk-api");
        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await RabbitMqTopology.DeclareAsync(_channel, ct);
        return _channel;
    }

    private async Task ResetAsync()
    {
        try { if (_channel is not null) await _channel.DisposeAsync(); } catch { /* yoksay */ }
        try { if (_connection is not null) await _connection.DisposeAsync(); } catch { /* yoksay */ }
        _channel = null;
        _connection = null;
    }

    public async ValueTask DisposeAsync() => await ResetAsync();
}
