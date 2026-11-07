namespace StajDesk.Domain.Common;

/// <summary>Bir iş kuralı ihlal edildiğinde fırlatılır (API'de 400 olarak döner).</summary>
public class DomainException(string message) : Exception(message);
