using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Features.Auth;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.Application.Features.Account;

/// <summary>
/// SMS ile tek kullanımlık kod: 6 hane, 5 dakika, en fazla 5 deneme. Kod veritabanında yalnızca
/// challenge kimliğiyle birlikte özetlenmiş (hash) olarak tutulur. Giriş ve iki adımlı doğrulamayı açma akışları ortak kullanır.
/// </summary>
public sealed class OtpService(IAppDbContext db, ISmsSender sms, IAuditLogger audit, ILogger<OtpService> logger)
{
    public const int TtlMinutes = 5;
    public const int MaxAttempts = 5;

    public bool IsAvailable => sms.IsConfigured;
    public string ProviderName => sms.ProviderName;

    public async Task<Guid> StartAsync(User user, OtpPurpose purpose, string? payload, CancellationToken ct)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = new OtpChallenge { UserId = user.Id, Purpose = purpose, Payload = payload, ExpiresAt = DateTime.UtcNow.AddMinutes(TtlMinutes) };
        challenge.CodeHash = Hash(challenge.Id, code);
        db.OtpChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);
        try
        {
            await sms.SendAsync(user.Phone!, $"StajDesk doğrulama kodunuz: {code}. {TtlMinutes} dakika geçerlidir. Bu kodu kimseyle paylaşmayın.", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "SMS gönderilemedi ({Provider})", sms.ProviderName);
            throw new OAuthException("temporarily_unavailable", "Doğrulama SMS'i şu anda gönderilemiyor. Birkaç dakika sonra tekrar deneyin.", 503);
        }
        audit.Add("auth.otp_sent", "user", user.Id, new { purpose = purpose.ToString() });
        await db.SaveChangesAsync(ct);
        return challenge.Id;
    }

    public async Task<OtpChallenge> ConsumeAsync(Guid challengeId, string code, OtpPurpose purpose, CancellationToken ct)
    {
        var c = await db.OtpChallenges.FirstOrDefaultAsync(x => x.Id == challengeId && x.Purpose == purpose, ct);
        if (c is null || c.ConsumedAt is not null || c.ExpiresAt < DateTime.UtcNow || c.Attempts >= MaxAttempts)
            throw new OAuthException("invalid_grant", "Doğrulama kodunun süresi doldu. Yeni kod isteyin.");
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(c.Id, code.Trim())), Encoding.ASCII.GetBytes(c.CodeHash));
        if (!ok)
        {
            c.Attempts++;
            await db.SaveChangesAsync(ct);
            var left = MaxAttempts - c.Attempts;
            throw new OAuthException("access_denied", left > 0 ? $"Kod hatalı. {left} deneme hakkınız kaldı." : "Çok fazla hatalı deneme. Yeni kod isteyin.", 401);
        }
        c.ConsumedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return c;
    }

    private static string Hash(Guid id, string code) => AuthService.Sha256($"{id}:{code}");
}
