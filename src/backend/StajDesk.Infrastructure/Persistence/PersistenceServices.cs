using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Contracts;
using StajDesk.Domain.Entities;

namespace StajDesk.Infrastructure.Persistence;

/// <summary>
/// Outbox'a yazan olay yayıncısı. Olay, çağıranın bir sonraki SaveChanges'inde iş verisiyle birlikte kaydedilir.
/// RabbitMQ'ya iletim arka planda <see cref="Messaging.OutboxDispatcher"/> tarafından yapılır.
/// </summary>
internal sealed class OutboxEventPublisher(AppDbContext db) : IEventPublisher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Enqueue<T>(T @event, string routingKey) where T : IntegrationEvent =>
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = @event.EventId,
            RoutingKey = routingKey,
            Payload = JsonSerializer.Serialize(@event, Json),
            OccurredAt = @event.OccurredAt,
        });
}

/// <summary>
/// Birden fazla SaveChanges'i tek transaction'da çalıştırır. Npgsql'in yeniden deneme stratejisiyle uyumlu olması için
/// işlem, execution strategy içinde yürütülür (geçici bağlantı hatasında baştan tekrar denenir).
/// </summary>
internal sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null) return work(ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async token =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(token);
            var result = await work(token);
            await tx.CommitAsync(token);
            return result;
        }, ct);
    }
}

/// <summary>Denetim kaydı ekler (çağıranın SaveChanges'i ile birlikte kaydedilir). Aktör ve IP isteğin kendisinden alınır.</summary>
internal sealed class AuditLogger(AppDbContext db, ICurrentUser currentUser) : IAuditLogger
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Add(string action, string entity, object? entityId, object? data = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = currentUser.UserId,
            Action = action,
            Entity = entity,
            EntityId = entityId?.ToString(),
            Data = data is null ? null : JsonSerializer.Serialize(data, Json),
            Ip = currentUser.IpAddress,
        });
}
