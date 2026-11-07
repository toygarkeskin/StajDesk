using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StajDesk.Contracts;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Worker;

/// <summary>
/// RabbitMQ'daki "stajdesk.notifications" kuyruğunu dinleyen arka plan servisi.
/// Mesaj başarıyla işlenirse ack, hata olursa nack gönderir.
/// Worker kapalıyken gelen mesajlar kuyrukta bekler (durable queue + persistent message) ve açılınca işlenir.
/// </summary>
public sealed class NotificationConsumer(
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> options,
    ILogger<NotificationConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectWithRetryAsync(stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel!);
        consumer.ReceivedAsync += (_, ea) => HandleAsync(ea, stoppingToken);

        // autoAck: false → mesajı biz onaylayana kadar RabbitMQ silmez.
        await _channel!.BasicConsumeAsync(Messaging.NotificationQueue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        logger.LogInformation("Kuyruk dinleniyor: {Queue}", Messaging.NotificationQueue);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { /* kapanıyor */ }
    }

    private async Task HandleAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var routingKey = ea.RoutingKey;
        var json = Encoding.UTF8.GetString(ea.Body.Span);
        try
        {
            IntegrationEvent? evt = routingKey switch
            {
                Messaging.RoutingKeys.TicketCreated => JsonSerializer.Deserialize<TicketCreatedEvent>(json, Json),
                Messaging.RoutingKeys.TicketStatusChanged => JsonSerializer.Deserialize<TicketStatusChangedEvent>(json, Json),
                Messaging.RoutingKeys.TicketAssigned => JsonSerializer.Deserialize<TicketAssignedEvent>(json, Json),
                Messaging.RoutingKeys.TicketCommentAdded => JsonSerializer.Deserialize<TicketCommentAddedEvent>(json, Json),
                _ => null
            };
            if (evt is null)
            {
                logger.LogWarning("Bilinmeyen mesaj tipi, ölü mektup kuyruğuna gönderiliyor: {RoutingKey}", routingKey);
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct);
                return;
            }

            await using var scope = scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<NotificationHandler>();
            await handler.HandleAsync(evt, ct);

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Bozuk mesaj, ölü mektup kuyruğuna gönderiliyor: {RoutingKey}", routingKey);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct);
        }
        catch (Exception ex)
        {
            // İlk hatada bir kez daha dene; tekrar teslimde de olmazsa DLQ'ya gönder (sonsuz döngüye girmesin).
            var requeue = !ea.Redelivered;
            logger.LogError(ex, "Mesaj işlenemedi ({RoutingKey}), requeue={Requeue}", routingKey, requeue);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: requeue, ct);
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        var factory = RabbitMqTopology.CreateFactory(options.Value, "stajdesk-worker");
        for (var attempt = 1; !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync(ct);
                _channel = await _connection.CreateChannelAsync(cancellationToken: ct);
                await RabbitMqTopology.DeclareAsync(_channel, ct);
                // Aynı anda en fazla 10 onaylanmamış mesaj al (adil dağıtım).
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, ct);
                logger.LogInformation("RabbitMQ bağlantısı kuruldu ({Host})", options.Value.Host);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, attempt * 3));
                logger.LogWarning("RabbitMQ'ya bağlanılamadı (deneme {Attempt}), {Delay} sn sonra tekrar: {Message}",
                    attempt, delay.TotalSeconds, ex.Message);
                await Task.Delay(delay, ct);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
