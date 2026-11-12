using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Contracts;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Auth;

// ---------- İstek / yanıt modelleri ----------

/// <summary>
/// Yetkilendirme isteği (RFC 6749 §4.1.1 + RFC 7636 PKCE) + kullanıcının kimlik bilgileri.
/// <see cref="Email"/> alanı geriye uyumluluk için bu adı taşır; e-posta veya kullanıcı adı olabilir.
/// </summary>
public sealed record AuthorizeRequest(string ResponseType, string ClientId, string RedirectUri, string CodeChallenge,
    string CodeChallengeMethod, string? Scope, string? State, string Email, string Password);

/// <summary>
/// Başarılı girişte <see cref="Code"/> döner. Kullanıcıda SMS doğrulaması açıksa kod yerine
/// <see cref="OtpRequired"/> = true, <see cref="ChallengeId"/> ve maskelenmiş telefon döner; kod SMS doğrulamasından sonra verilir.
/// </summary>
public sealed record AuthorizeResponse(string? Code, string? State, string RedirectUri, bool OtpRequired = false, Guid? ChallengeId = null, string? MaskedPhone = null);
public sealed record VerifyLoginOtpRequest(Guid ChallengeId, string Code);
public sealed record VerifyEmailRequest(string Token);
public sealed record ResendVerificationRequest(string Email);

/// <summary>Token isteği (RFC 6749 §4.1.3, §4.4.2, §6).</summary>
public sealed record TokenRequest(string GrantType, string? Code, string? CodeVerifier, string? RedirectUri, string? RefreshToken,
    string? Scope, string ClientId, string? ClientSecret);

public sealed record TokenResult(string AccessToken, string TokenType, int ExpiresIn, string Scope, string? RefreshToken, bool FirstParty);

public sealed record RegisterRequest(string FullName, string Email, string Password);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string Password);

/// <summary>OAuth 2.0 (RFC 6749 §5.2) hata yanıtı: { error, error_description }.</summary>
public sealed class OAuthException(string error, string description, int status = 400) : Exception(description)
{
    public string Error { get; } = error;
    public int Status { get; } = status;
}

public interface IAuthService
{
    Task<AuthorizeResponse> AuthorizeAsync(AuthorizeRequest request, CancellationToken ct = default);
    Task<TokenResult> TokenAsync(TokenRequest request, string? userAgent, CancellationToken ct = default);
    Task RevokeAsync(string? token, string clientId, CancellationToken ct = default);
    Task<int> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task RequestPasswordResetAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<AuthorizeResponse> VerifyLoginOtpAsync(VerifyLoginOtpRequest request, CancellationToken ct = default);
    Task VerifyEmailAsync(string token, CancellationToken ct = default);
    Task ResendVerificationAsync(string email, CancellationToken ct = default);
}

public sealed class AuthOptions
{
    public int RefreshTokenDays { get; set; } = 14;
    public string WebUrl { get; set; } = "http://localhost:8080";
    public int LoginMaxFailed { get; set; } = 5;
    public int LoginWindowSeconds { get; set; } = 300;
    /// <summary>
    /// Aynı refresh token birkaç saniye içinde ikinci kez gelirse (iki sekme aynı anda yeniledi) bu saldırı sayılmaz.
    /// Süre dışındaki tekrar kullanım çalınma belirtisidir ve tüm oturum zinciri iptal edilir.
    /// </summary>
    public int RefreshReuseGraceSeconds { get; set; } = 20;
}

/// <summary>
/// OAuth 2.0 yetkilendirme sunucusu + hesap işlemleri:
///  - Authorization Code + PKCE (S256 zorunlu) — tarayıcı uygulaması (public client)
///  - Refresh Token rotasyonu ve yeniden kullanım tespiti (OAuth 2.0 Security BCP §4.14)
///  - Client Credentials — servisten servise (kapsam = izin anahtarı)
///  - Revocation (RFC 7009)
/// "Password grant" kasıtlı olarak yoktur (OAuth 2.1'de kaldırıldı).
/// </summary>
internal sealed partial class AuthService(
    IAppDbContext db,
    IPasswordHasher hasher,
    IAccessTokenIssuer tokens,
    IRateLimiter limiter,
    IEventPublisher events,
    IAuditLogger audit,
    ICurrentUser currentUser,
    Microsoft.Extensions.Options.IOptions<AuthOptions> options,
    ILogger<AuthService> logger,
    Account.OtpService otp) : IAuthService
{
    public const string LoginFailedMessage = "Kullanıcı adı / e-posta ve şifre eşleşmedi. Bilgileri kontrol edip tekrar deneyin.";
    private static readonly string[] UserScopes = ["openid", "profile", "api"];
    private const int CodeTtlSeconds = 60;
    private const int ResetTtlMinutes = 30;

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")] private static partial Regex ChallengeFormat();
    [GeneratedRegex(@"^[A-Za-z0-9\-._~]{43,128}$")] private static partial Regex VerifierFormat();

    public static string Sha256(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string RandomToken(int bytes = 32) => Base64Url(RandomNumberGenerator.GetBytes(bytes));
    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>RFC 7636 §4.6: BASE64URL(SHA256(code_verifier)) == code_challenge (sabit zamanlı karşılaştırma).</summary>
    public static bool VerifyPkce(string verifier, string challenge)
    {
        if (!VerifierFormat().IsMatch(verifier)) return false;
        var computed = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(challenge));
    }

    // ----- 1) Yetkilendirme: kimlik doğrula, 60 sn'lik tek kullanımlık kod üret -----
    public async Task<AuthorizeResponse> AuthorizeAsync(AuthorizeRequest r, CancellationToken ct = default)
    {
        if (r.ResponseType != "code") throw new OAuthException("unsupported_response_type", "Yalnızca response_type=code desteklenir.");
        var client = await db.OAuthClients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientId == r.ClientId, ct);
        if (client is null || !client.GrantTypes.Contains("authorization_code")) throw new OAuthException("invalid_client", "Tanınmayan istemci.", 401);
        if (!client.RedirectUris.Contains(r.RedirectUri)) throw new OAuthException("invalid_request", "redirect_uri bu istemci için kayıtlı değil.");
        if (r.CodeChallengeMethod != "S256") throw new OAuthException("invalid_request", "PKCE zorunludur ve code_challenge_method=S256 olmalıdır.");
        if (!ChallengeFormat().IsMatch(r.CodeChallenge)) throw new OAuthException("invalid_request", "Geçersiz code_challenge.");
        var scope = UserScope(r.Scope);

        var user = await AuthenticateAsync(r.Email, r.Password, ct);

        // İkinci adım: SMS doğrulaması (yalnızca sağlayıcı yapılandırılmışsa; aksi hâlde uyarı loglanır).
        if (user.TwoFactorEnabled)
        {
            if (otp.IsAvailable && !string.IsNullOrWhiteSpace(user.Phone))
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new PendingAuthorize(client.ClientId, r.RedirectUri, r.CodeChallenge, r.CodeChallengeMethod, scope));
                var challenge = await otp.StartAsync(user, OtpPurpose.Login, payload, ct);
                return new AuthorizeResponse(null, r.State, r.RedirectUri, true, challenge, Account.AccountLinks.MaskPhone(user.Phone!));
            }
            logger.LogWarning("Kullanıcıda SMS doğrulaması açık ama SMS sağlayıcısı yapılandırılmamış; tek adımlı giriş yapıldı: {UserId}", user.Id);
        }
        return new AuthorizeResponse(await IssueCodeAsync(client.ClientId, user.Id, r.RedirectUri, r.CodeChallenge, r.CodeChallengeMethod, scope, ct), r.State, r.RedirectUri);
    }

    private sealed record PendingAuthorize(string ClientId, string RedirectUri, string CodeChallenge, string CodeChallengeMethod, string Scope);

    private async Task<string> IssueCodeAsync(string clientId, int userId, string redirectUri, string challenge, string method, string scope, CancellationToken ct)
    {
        var code = RandomToken();
        db.AuthorizationCodes.Add(new AuthorizationCode
        {
            CodeHash = Sha256(code), ClientId = clientId, UserId = userId, RedirectUri = redirectUri,
            CodeChallenge = challenge, CodeChallengeMethod = method, Scope = scope,
            ExpiresAt = DateTime.UtcNow.AddSeconds(CodeTtlSeconds),
        });
        await db.SaveChangesAsync(ct);
        return code;
    }

    public async Task<AuthorizeResponse> VerifyLoginOtpAsync(VerifyLoginOtpRequest r, CancellationToken ct = default)
    {
        var c = await otp.ConsumeAsync(r.ChallengeId, r.Code, OtpPurpose.Login, ct);
        var p = System.Text.Json.JsonSerializer.Deserialize<PendingAuthorize>(c.Payload!)!;
        audit.Add("auth.otp_verified", "user", c.UserId);
        var code = await IssueCodeAsync(p.ClientId, c.UserId, p.RedirectUri, p.CodeChallenge, p.CodeChallengeMethod, p.Scope, ct);
        return new AuthorizeResponse(code, null, p.RedirectUri);
    }

    // ----- 2) Token uç noktası -----
    public async Task<TokenResult> TokenAsync(TokenRequest r, string? userAgent, CancellationToken ct = default)
    {
        var client = await AuthenticateClientAsync(r.ClientId, r.ClientSecret, ct);
        if (!client.GrantTypes.Contains(r.GrantType)) throw new OAuthException("unauthorized_client", $"Bu istemci \"{r.GrantType}\" akışını kullanamaz.");
        return r.GrantType switch
        {
            "authorization_code" => await ExchangeCodeAsync(client, r, userAgent, ct),
            "refresh_token" => await RefreshAsync(client, r.RefreshToken, userAgent, ct),
            "client_credentials" => ClientCredentials(client, r.Scope),
            _ => throw new OAuthException("unsupported_grant_type", "Desteklenmeyen grant_type."),
        };
    }

    /// <summary>RFC 7009: token geçersiz olsa da hata vermez (bilgi sızdırmaz).</summary>
    public async Task RevokeAsync(string? token, string clientId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token)) return;
        var hash = Sha256(token);
        var rec = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (rec is null || rec.ClientId != clientId) return;
        await RevokeFamilyAsync(rec.FamilyId, ct);
        audit.Add("auth.logout", "session", rec.FamilyId, null);
        await db.SaveChangesAsync(ct);
    }

    // ----- Hesap işlemleri -----
    public async Task<int> RegisterAsync(RegisterRequest r, CancellationToken ct = default)
    {
        var email = r.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Bu e-posta adresiyle kayıtlı bir hesap var. Giriş yapmayı veya şifre sıfırlamayı deneyin.");
        var role = await db.Roles.FirstAsync(x => x.Name == RoleNames.User, ct);
        var user = new User { FullName = Text.Clean(r.FullName), Email = email, PasswordHash = hasher.Hash(r.Password) };
        user.Roles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        // E-posta doğrulanana kadar giriş yapılamaz: bağlantı hoş geldiniz e-postasında gider.
        var verifyUrl = Account.AccountLinks.VerifyUrl(db, options.Value.WebUrl, user.Id);
        events.Enqueue(new UserRegisteredEvent(user.Id, user.Email, user.FullName, verifyUrl), Messaging.RoutingKeys.UserRegistered);
        audit.Add("user.registered", "user", user.Id);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Yeni kullanıcı kaydoldu: {UserId}", user.Id);
        return user.Id;
    }

    /// <summary>Hesap var olsun ya da olmasın aynı yanıt döner (hesap varlığı sızdırılmaz).</summary>
    public async Task RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var key = $"reset:{normalized}";
        if (await limiter.GetBlockedSecondsAsync(key, ct) is not null) return;
        await limiter.RegisterHitAsync(key, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized && u.IsActive, ct);
        if (user is null)
        {
            logger.LogInformation("Şifre sıfırlama istendi (hesap yok veya pasif)");
            return;
        }
        var token = RandomToken();
        db.PasswordResetTokens.Add(new PasswordResetToken { TokenHash = Sha256(token), UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(ResetTtlMinutes) });
        events.Enqueue(new PasswordResetRequestedEvent(user.Id, user.Email, user.FullName,
            $"{options.Value.WebUrl.TrimEnd('/')}/reset-password?token={token}", ResetTtlMinutes), Messaging.RoutingKeys.PasswordResetRequested);
        audit.Add("user.password_reset_requested", "user", user.Id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Şifre sıfırlama VE davetle gelen ilk şifre belirleme. E-posta bağlantısına tıklamak adresin sahipliğini kanıtladığı için hesap doğrulanmış sayılır.</summary>
    public async Task ResetPasswordAsync(ResetPasswordRequest r, CancellationToken ct = default)
    {
        var hash = Sha256(r.Token);
        var now = DateTime.UtcNow;
        var rec = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now
                      && (t.Purpose == AccountTokenPurpose.PasswordReset || t.Purpose == AccountTokenPurpose.Invitation), ct)
                  ?? throw new DomainExceptionAdapter("Bu bağlantı geçersiz veya süresi dolmuş. Yeni bir bağlantı isteyin.");
        var user = await db.Users.FirstAsync(u => u.Id == rec.UserId, ct);
        if (!user.IsActive) throw new DomainExceptionAdapter("Hesabınız pasif durumda. İnsan Kaynakları ile iletişime geçin.");
        rec.UsedAt = now;
        user.PasswordHash = hasher.Hash(r.Password);
        user.EmailVerifiedAt ??= now;
        user.UpdatedAt = now;
        // Şifre değişince açık tüm oturumlar kapatılır.
        foreach (var t in await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct)) t.RevokedAt = now;
        audit.Add(rec.Purpose == AccountTokenPurpose.Invitation ? "user.first_password_set" : "user.password_reset", "user", user.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        var hash = Sha256(token);
        var now = DateTime.UtcNow;
        var rec = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now
                      && t.Purpose == AccountTokenPurpose.EmailVerification, ct)
                  ?? throw new DomainExceptionAdapter("Doğrulama bağlantısı geçersiz veya süresi dolmuş. Giriş ekranından yeni bağlantı isteyin.");
        var user = await db.Users.FirstAsync(u => u.Id == rec.UserId, ct);
        rec.UsedAt = now;
        user.EmailVerifiedAt ??= now;
        audit.Add("user.email_verified", "user", user.Id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Hesap var olsun ya da olmasın aynı yanıt; hız sınırlı.</summary>
    public async Task ResendVerificationAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var key = $"verify:{normalized}";
        if (await limiter.GetBlockedSecondsAsync(key, ct) is not null) return;
        await limiter.RegisterHitAsync(key, ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized && u.IsActive && u.EmailVerifiedAt == null, ct);
        if (user is null) return;
        var verifyUrl = Account.AccountLinks.VerifyUrl(db, options.Value.WebUrl, user.Id);
        events.Enqueue(new UserRegisteredEvent(user.Id, user.Email, user.FullName, verifyUrl), Messaging.RoutingKeys.UserRegistered);
        await db.SaveChangesAsync(ct);
    }

    // ----- yardımcılar -----
    /// <summary>Kullanıcı adı veya e-posta + şifre. "@" içeriyorsa e-posta, değilse kullanıcı adı olarak aranır.</summary>
    private async Task<User> AuthenticateAsync(string login, string password, CancellationToken ct)
    {
        var normalized = login.Trim().ToLowerInvariant();
        string[] keys = [$"login:{normalized}", $"login-ip:{currentUser.IpAddress ?? "unknown"}"];
        foreach (var key in keys)
        {
            if (await limiter.GetBlockedSecondsAsync(key, ct) is { } wait)
            {
                logger.LogWarning("Giriş hız sınırına takıldı: {Email}", normalized);
                throw new TooManyRequestsException($"Bu hesap için çok sayıda başarısız deneme oldu. {Math.Max(1, wait / 60)} dakika sonra tekrar deneyin.", wait);
            }
        }

        var user = normalized.Contains('@')
            ? await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct)
            : await db.Users.FirstOrDefaultAsync(u => u.Username == normalized, ct);
        // Kullanıcı yoksa da bir hash doğrulaması yapılır: yanıt süresinden hesap varlığı anlaşılmasın.
        var ok = user is not null ? hasher.Verify(user.PasswordHash, password) : hasher.Verify(DummyHash, password) && false;
        if (user is null || !ok || !user.IsActive)
        {
            foreach (var key in keys) await limiter.RegisterHitAsync(key, ct);
            audit.Add("auth.login_failed", "user", user?.Id, new { email = normalized });
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Başarısız giriş denemesi: {Email}", normalized);
            throw new OAuthException("access_denied", LoginFailedMessage, 401);
        }

        foreach (var key in keys) await limiter.ResetAsync(key, ct);
        if (user.EmailVerifiedAt is null)
        {
            // Şifre doğru olduktan SONRA söylenir: hesap varlığı şifresiz öğrenilemez.
            throw new OAuthException("email_not_verified", "E-posta adresiniz henüz doğrulanmadı. Gelen kutunuzdaki bağlantıya tıklayın veya yeni bağlantı isteyin.", 403);
        }
        user.LastLoginAt = DateTime.UtcNow;
        audit.Add("auth.login", "user", user.Id);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Kullanıcı giriş yaptı: {UserId}", user.Id);
        return user;
    }

    private async Task<OAuthClient> AuthenticateClientAsync(string clientId, string? secret, CancellationToken ct)
    {
        var client = await db.OAuthClients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientId == clientId, ct)
                     ?? throw new OAuthException("invalid_client", "Tanınmayan istemci.", 401);
        if (client.SecretHash is not null && (secret is null || !hasher.Verify(client.SecretHash, secret)))
            throw new OAuthException("invalid_client", "İstemci kimlik doğrulaması başarısız.", 401);
        return client;
    }

    private async Task<TokenResult> ExchangeCodeAsync(OAuthClient client, TokenRequest r, string? userAgent, CancellationToken ct)
    {
        if (r.Code is null || r.CodeVerifier is null || r.RedirectUri is null)
            throw new OAuthException("invalid_request", "code, code_verifier ve redirect_uri zorunludur.");
        var now = DateTime.UtcNow;
        var hash = Sha256(r.Code);
        // Tek kullanımlık: yalnızca henüz tüketilmemişse işaretle (eşzamanlı iki istekte biri kazanır).
        var consumed = await db.AuthorizationCodes.Where(c => c.CodeHash == hash && c.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ConsumedAt, now), ct);
        var rec = consumed == 1 ? await db.AuthorizationCodes.AsNoTracking().FirstAsync(c => c.CodeHash == hash, ct) : null;
        if (rec is null || rec.ExpiresAt < now) throw new OAuthException("invalid_grant", "Yetkilendirme kodu geçersiz, süresi dolmuş veya daha önce kullanılmış.");
        if (rec.ClientId != client.ClientId || rec.RedirectUri != r.RedirectUri) throw new OAuthException("invalid_grant", "Kod bu istemci veya redirect_uri için verilmedi.");
        if (!VerifyPkce(r.CodeVerifier, rec.CodeChallenge)) throw new OAuthException("invalid_grant", "PKCE doğrulaması başarısız.");
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == rec.UserId && u.IsActive, ct)
                   ?? throw new OAuthException("invalid_grant", "Hesap pasif.");
        return await IssueUserTokensAsync(client, user, rec.Scope, Guid.NewGuid(), null, userAgent, ct);
    }

    private async Task<TokenResult> RefreshAsync(OAuthClient client, string? token, string? userAgent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token)) throw new OAuthException("invalid_request", "refresh_token zorunludur.");
        var hash = Sha256(token);
        var rec = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (rec is null || rec.ClientId != client.ClientId) throw new OAuthException("invalid_grant", "Oturum bulunamadı. Tekrar giriş yapın.");
        if (rec.RevokedAt is not null && rec.ReplacedBy is not null && (DateTime.UtcNow - rec.RevokedAt.Value).TotalSeconds <= options.Value.RefreshReuseGraceSeconds)
        {
            // Eşzamanlı yenileme (çoklu sekme): zincir hâlâ açıksa aynı aileden yeni token ver, eskisine dokunma.
            var familyAlive = await db.RefreshTokens.AnyAsync(t => t.FamilyId == rec.FamilyId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow, ct);
            if (!familyAlive) throw new OAuthException("invalid_grant", "Oturum sonlandırılmış. Tekrar giriş yapın.");
            var concurrentUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == rec.UserId && u.IsActive, ct)
                                 ?? throw new OAuthException("invalid_grant", "Hesap pasif.");
            return await IssueUserTokensAsync(client, concurrentUser, rec.Scope, rec.FamilyId, null, userAgent, ct);
        }
        if (rec.RevokedAt is not null)
        {
            // Daha önce döndürülmüş bir token tekrar geldi → çalınmış olabilir: tüm zinciri iptal et.
            await RevokeFamilyAsync(rec.FamilyId, ct);
            audit.Add("auth.refresh_reuse_detected", "session", rec.FamilyId, new { rec.UserId });
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Refresh token yeniden kullanımı tespit edildi; oturum zinciri iptal edildi: {UserId}", rec.UserId);
            throw new OAuthException("invalid_grant", "Oturum güvenlik nedeniyle sonlandırıldı. Tekrar giriş yapın.");
        }
        if (rec.ExpiresAt < DateTime.UtcNow) throw new OAuthException("invalid_grant", "Oturumun süresi doldu. Tekrar giriş yapın.");
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == rec.UserId && u.IsActive, ct);
        if (user is null)
        {
            await RevokeFamilyAsync(rec.FamilyId, ct);
            throw new OAuthException("invalid_grant", "Hesap pasif.");
        }
        return await IssueUserTokensAsync(client, user, rec.Scope, rec.FamilyId, rec, userAgent, ct);
    }

    private TokenResult ClientCredentials(OAuthClient client, string? requested)
    {
        if (client.SecretHash is null) throw new OAuthException("unauthorized_client", "client_credentials yalnızca gizli istemciler içindir.");
        var asked = string.IsNullOrWhiteSpace(requested) ? client.Scopes : requested.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var invalid = asked.Where(s => !client.Scopes.Contains(s) || !Permissions.IsKnown(s)).ToList();
        if (invalid.Count > 0) throw new OAuthException("invalid_scope", $"İzin verilmeyen kapsam: {string.Join(", ", invalid)}");
        var scope = string.Join(' ', asked);
        var access = tokens.Issue(new AccessTokenClaims(null, client.ClientId, scope, [], client.Name, null));
        return new TokenResult(access, "Bearer", tokens.ExpiresInSeconds, scope, null, false);
    }

    private async Task<TokenResult> IssueUserTokensAsync(OAuthClient client, User user, string scope, Guid familyId, RefreshToken? rotateFrom, string? userAgent, CancellationToken ct)
    {
        var roles = await db.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.Role.Name).ToListAsync(ct);
        var access = tokens.Issue(new AccessTokenClaims(user.Id, client.ClientId, scope, roles, user.FullName, familyId));
        var refresh = RandomToken();
        var next = new RefreshToken
        {
            TokenHash = Sha256(refresh), UserId = user.Id, ClientId = client.ClientId, FamilyId = familyId, Scope = scope,
            ExpiresAt = DateTime.UtcNow.AddDays(options.Value.RefreshTokenDays),
            UserAgent = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent, Ip = currentUser.IpAddress,
        };
        db.RefreshTokens.Add(next);
        if (rotateFrom is not null)
        {
            rotateFrom.RevokedAt = DateTime.UtcNow;
            rotateFrom.ReplacedBy = next.Id;
        }
        await db.SaveChangesAsync(ct);
        return new TokenResult(access, "Bearer", tokens.ExpiresInSeconds, scope, refresh, client.IsFirstParty);
    }

    /// <summary>Oturum zincirini iptal eder (idempotent; bir zincirde birkaç token olur).</summary>
    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var t in await db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(ct))
            t.RevokedAt = now;
        await db.SaveChangesAsync(ct);
    }

    private static string UserScope(string? requested)
    {
        var asked = (requested ?? string.Join(' ', UserScopes)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var invalid = asked.Where(s => !UserScopes.Contains(s)).ToList();
        if (invalid.Count > 0) throw new OAuthException("invalid_scope", $"Geçersiz kapsam: {string.Join(", ", invalid)}");
        return string.Join(' ', asked);
    }

    /// <summary>Var olmayan kullanıcı için zamanlama eşitleme amaçlı sabit (geçerli bir şifreye karşılık gelmeyen) hash.</summary>
    private const string DummyHash = "AQAAAAIAAYagAAAAEMZsS8xR7uTqv0k3Yk5bGkq2q3o0Jf0rT0m0U0f0rP0eZ2q3o0Jf0rT0m0U0f0rP0eZ2q3o0Jf0rT0==";
}

/// <summary>Domain hatasını uygulama katmanından fırlatmak için (400).</summary>
public sealed class DomainExceptionAdapter(string message) : Domain.Common.DomainException(message);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Adınızı ve soyadınızı girin.").MaximumLength(100).NoMarkup();
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta adresinizi girin.").EmailAddress().WithMessage("ad@sirket.com biçiminde bir e-posta girin.").MaximumLength(150);
        RuleFor(x => x.Password).SetValidator(new PasswordRule());
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() =>
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta adresinizi girin.").EmailAddress().WithMessage("ad@sirket.com biçiminde bir e-posta girin.");
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Sıfırlama bağlantısı eksik. E-postadaki bağlantıyı yeniden açın.");
        RuleFor(x => x.Password).SetValidator(new PasswordRule());
    }
}
