namespace StajDesk.Application.Common;

/// <summary>Uygulama katmanının fırlattığı hatalar; global hata middleware'i bunları HTTP durum kodlarına çevirir.</summary>
public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string message) : AppException(message)
{
    public static NotFoundException For(string entity, object id) => new($"{entity} bulunamadı (#{id}).");
}

public sealed class ForbiddenException(string message = "Bu işlem rolünüze açık değil. Erişim gerekiyorsa yöneticinizden isteyin.") : AppException(message);

public sealed class UnauthorizedException(string message) : AppException(message);

public sealed class ConflictException(string message) : AppException(message);

public sealed class TooManyRequestsException(string message, int retryAfterSeconds) : AppException(message)
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
