namespace StajDesk.Domain.Entities;

/// <summary>
/// Transactional outbox: olay, iş verisiyle aynı transaction'da yazılır; arka plan dağıtıcısı RabbitMQ'ya iletir.
/// Böylece "veri kaydedildi ama mesaj kayboldu" durumu oluşmaz.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoutingKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Worker idempotency: aynı mesaj ikinci kez gelirse işlenmez.</summary>
public class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Demo için: gönderilen her e-postanın kaydı (Mailpit'e de gerçekten SMTP ile gider).</summary>
public class SentEmail
{
    public int Id { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string Status { get; set; } = "sent";
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Güvenlik açısından önemli işlemlerin denetim kaydı (giriş, rol değişikliği, silme...).</summary>
public class AuditLog
{
    public long Id { get; set; }
    public int? ActorId { get; set; }
    public User? Actor { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Data { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
