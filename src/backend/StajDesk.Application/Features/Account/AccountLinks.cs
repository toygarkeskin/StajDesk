using StajDesk.Application.Abstractions;
using StajDesk.Contracts;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.Application.Features.Account;

/// <summary>
/// Tek kullanımlık hesap bağlantıları (davet, e-posta doğrulama). Token yalnızca e-postada düz metin olarak gider;
/// veritabanında SHA-256 özeti saklanır.
/// </summary>
public static class AccountLinks
{
    public const int InvitationHours = 72;
    public const int VerificationHours = 24;

    public static string NewToken(IAppDbContext db, int userId, AccountTokenPurpose purpose, TimeSpan ttl)
    {
        var token = Auth.AuthService.RandomToken();
        db.PasswordResetTokens.Add(new PasswordResetToken { TokenHash = Auth.AuthService.Sha256(token), UserId = userId, Purpose = purpose, ExpiresAt = DateTime.UtcNow.Add(ttl) });
        return token;
    }

    /// <summary>İK / yönetici hesabı oluşturunca: "ilk şifreni belirle" e-postası (outbox üzerinden).</summary>
    public static void Invite(IAppDbContext db, IEventPublisher events, string webUrl, User user, string invitedBy)
    {
        var token = NewToken(db, user.Id, AccountTokenPurpose.Invitation, TimeSpan.FromHours(InvitationHours));
        events.Enqueue(new UserInvitedEvent(user.Id, user.Email, user.FullName,
            $"{webUrl.TrimEnd('/')}/set-password?token={token}", InvitationHours, invitedBy), Messaging.RoutingKeys.UserInvited);
    }

    public static string VerifyUrl(IAppDbContext db, string webUrl, int userId)
    {
        var token = NewToken(db, userId, AccountTokenPurpose.EmailVerification, TimeSpan.FromHours(VerificationHours));
        return $"{webUrl.TrimEnd('/')}/verify-email?token={token}";
    }

    /// <summary>Telefonun yalnızca son iki hanesi gösterilir: 0555 *** ** 12</summary>
    public static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length < 4 ? "***" : $"{digits[..Math.Min(4, digits.Length - 2)]} *** ** {digits[^2..]}";
    }
}
