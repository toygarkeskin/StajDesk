using StajDesk.Application.Features.Account;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Auth;
using StajDesk.Application.Features.Tickets;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>OAuth 2.0 (PKCE, istemci doğrulama, kapsam), hız sınırı, XSS temizliği ve izin matrisi.</summary>
public class SecurityTests
{
    private static string Challenge(string verifier) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk-abcdef";

    // ---------- PKCE (RFC 7636) ----------

    [Fact]
    public void Pkce_accepts_matching_verifier() => Assert.True(AuthService.VerifyPkce(Verifier, Challenge(Verifier)));

    [Fact]
    public void Pkce_rejects_wrong_or_short_verifier()
    {
        Assert.False(AuthService.VerifyPkce(Verifier + "x", Challenge(Verifier)));
        Assert.False(AuthService.VerifyPkce("kisa", Challenge("kisa")));
    }

    // ---------- Yetkilendirme sunucusu ----------

    private sealed class Fixture
    {
        public TestDbContext Db { get; } = TestDbContext.Create();
        public FakeCurrentUser Current { get; } = new();
        public InMemoryRateLimiter Limiter { get; } = new(max: 3);
        public FakeTokenIssuer Tokens { get; } = new();
        public FakeSms Sms { get; } = new();
        public FakeEventPublisher Events { get; } = new();
        public AuthService Auth { get; }
        public OtpService Otp { get; }

        public Fixture(int graceSeconds = 20)
        {
            var roles = Db.SeedRoles();
            Db.OAuthClients.Add(new OAuthClient { ClientId = "web", Name = "Web", IsFirstParty = true, RedirectUris = ["http://app/cb"], GrantTypes = ["authorization_code", "refresh_token"] });
            Db.OAuthClients.Add(new OAuthClient { ClientId = "svc", Name = "Servis", SecretHash = "h:s3cret", GrantTypes = ["client_credentials"], Scopes = [Permissions.TicketsReadAll] });
            var user = new User { FullName = "Ayşe", Email = "ayse@test", Username = "ayse", Phone = "05551112233", PasswordHash = "h:Dogru!Sifre1", EmailVerifiedAt = DateTime.UtcNow };
            user.Roles.Add(new UserRole { Role = roles[RoleNames.User] });
            Db.Users.Add(user);
            Db.SaveChanges();
            var audit = new NoAudit();
            Otp = new OtpService(Db, Sms, audit, NullLogger<OtpService>.Instance);
            Auth = new AuthService(Db, new FakeHasher(), Tokens, Limiter, Events, audit, Current,
                Options.Create(new AuthOptions { RefreshReuseGraceSeconds = graceSeconds }), NullLogger<AuthService>.Instance, Otp);
        }

        /// <summary>Girişten sonraki durumu kurar: veritabanında bir oturum zinciri ve ona ait refresh token.</summary>
        public Task<string> SignInAsync()
        {
            var raw = AuthService.RandomToken();
            Db.RefreshTokens.Add(new RefreshToken
            {
                TokenHash = AuthService.Sha256(raw), UserId = Db.Users.Single().Id, ClientId = "web", FamilyId = Guid.NewGuid(),
                Scope = "openid profile api", ExpiresAt = DateTime.UtcNow.AddDays(1),
            });
            Db.SaveChanges();
            return Task.FromResult(raw);
        }

        public Task<TokenResult> RefreshAsync(string token) =>
            Auth.TokenAsync(new TokenRequest("refresh_token", null, null, null, token, null, "web", null), null);

        public AuthorizeRequest Login(string password = "Dogru!Sifre1", string redirect = "http://app/cb", string method = "S256", string login = "ayse@test") =>
            new("code", "web", redirect, Challenge(Verifier), method, null, "xyz", login, password);
    }

    [Fact]
    public async Task Concurrent_refresh_from_two_tabs_keeps_session()
    {
        var f = new Fixture();
        var rt = await f.SignInAsync();
        await f.RefreshAsync(rt);                 // 1. sekme yeniledi
        var second = await f.RefreshAsync(rt);    // 2. sekme aynı anda eski token'ı gönderdi
        Assert.NotNull(second.RefreshToken);
        Assert.Contains(f.Db.RefreshTokens, t => t.RevokedAt == null); // oturum açık kaldı
    }

    [Fact]
    public async Task Refresh_token_reuse_after_grace_revokes_whole_session()
    {
        var f = new Fixture(graceSeconds: 0);
        var rt = await f.SignInAsync();
        var next = await f.RefreshAsync(rt);
        await Task.Delay(20);
        await Assert.ThrowsAsync<OAuthException>(() => f.RefreshAsync(rt));            // çalınmış token
        await Assert.ThrowsAsync<OAuthException>(() => f.RefreshAsync(next.RefreshToken!)); // meşru zincir de iptal
        Assert.DoesNotContain(f.Db.RefreshTokens, t => t.RevokedAt == null);
    }

    [Fact]
    public async Task Grace_window_does_not_revive_logged_out_session()
    {
        var f = new Fixture();
        var rt = await f.SignInAsync();
        var next = await f.RefreshAsync(rt);
        await f.Auth.RevokeAsync(next.RefreshToken, "web"); // çıkış yapıldı
        await Assert.ThrowsAsync<OAuthException>(() => f.RefreshAsync(rt));
    }

    [Fact]
    public async Task Authorize_issues_single_use_code_and_keeps_state()
    {
        var f = new Fixture();
        var res = await f.Auth.AuthorizeAsync(f.Login());
        Assert.Equal("xyz", res.State);
        Assert.Single(f.Db.AuthorizationCodes);
        // Kod veritabanında düz metin değil, özet (hash) olarak saklanır.
        Assert.NotEqual(res.Code, f.Db.AuthorizationCodes.Single().CodeHash);
    }

    [Fact]
    public async Task Authorize_rejects_unregistered_redirect_uri_and_plain_pkce()
    {
        var f = new Fixture();
        var bad = await Assert.ThrowsAsync<OAuthException>(() => f.Auth.AuthorizeAsync(f.Login(redirect: "http://evil/cb")));
        Assert.Equal("invalid_request", bad.Error);
        var plain = await Assert.ThrowsAsync<OAuthException>(() => f.Auth.AuthorizeAsync(f.Login(method: "plain")));
        Assert.Equal("invalid_request", plain.Error);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_and_rate_limited()
    {
        var f = new Fixture();
        for (var i = 0; i < 3; i++)
        {
            var ex = await Assert.ThrowsAsync<OAuthException>(() => f.Auth.AuthorizeAsync(f.Login("yanlis")));
            Assert.Equal("access_denied", ex.Error);
        }
        // Limit dolunca doğru şifre de pencere bitene kadar reddedilir.
        await Assert.ThrowsAsync<TooManyRequestsException>(() => f.Auth.AuthorizeAsync(f.Login()));
    }

    [Fact]
    public async Task Client_credentials_requires_secret_and_allowed_scope()
    {
        var f = new Fixture();
        var ok = await f.Auth.TokenAsync(new TokenRequest("client_credentials", null, null, null, null, Permissions.TicketsReadAll, "svc", "s3cret"), null);
        Assert.Equal(Permissions.TicketsReadAll, ok.Scope);
        Assert.Null(ok.RefreshToken);
        Assert.Null(f.Tokens.Last!.UserId);

        var badSecret = await Assert.ThrowsAsync<OAuthException>(() => f.Auth.TokenAsync(new TokenRequest("client_credentials", null, null, null, null, null, "svc", "yanlis"), null));
        Assert.Equal("invalid_client", badSecret.Error);
        var badScope = await Assert.ThrowsAsync<OAuthException>(() => f.Auth.TokenAsync(new TokenRequest("client_credentials", null, null, null, null, Permissions.UsersManage, "svc", "s3cret"), null));
        Assert.Equal("invalid_scope", badScope.Error);
    }

    [Fact]
    public async Task Public_client_cannot_use_client_credentials()
    {
        var f = new Fixture();
        var ex = await Assert.ThrowsAsync<OAuthException>(() => f.Auth.TokenAsync(new TokenRequest("client_credentials", null, null, null, null, null, "web", null), null));
        Assert.Equal("unauthorized_client", ex.Error);
    }

    [Fact]
    public async Task Register_gives_user_role_and_rejects_duplicate_email()
    {
        var f = new Fixture();
        var id = await f.Auth.RegisterAsync(new RegisterRequest("Yeni Kişi", "Yeni@Test", "Guclu!Sifre1"));
        Assert.Equal(RoleNames.User, f.Db.UserRoles.Where(r => r.UserId == id).Select(r => f.Db.Roles.First(x => x.Id == r.RoleId).Name).Single());
        await Assert.ThrowsAsync<ConflictException>(() => f.Auth.RegisterAsync(new RegisterRequest("Tekrar", "yeni@test", "Guclu!Sifre1")));
    }

    // ---------- Kimlik: kullanıcı adı, e-posta doğrulama, davet, SMS OTP ----------

    [Fact]
    public async Task Login_accepts_username_as_well_as_email()
    {
        var f = new Fixture();
        var byUsername = await f.Auth.AuthorizeAsync(f.Login(login: "AYSE"));
        Assert.NotNull(byUsername.Code);
    }

    [Fact]
    public async Task Self_registered_user_must_verify_email_before_login()
    {
        var f = new Fixture();
        await f.Auth.RegisterAsync(new RegisterRequest("Yeni Kişi", "yeni@test", "Dogru!Sifre1"));
        var ex = await Assert.ThrowsAsync<OAuthException>(() => f.Auth.AuthorizeAsync(f.Login(login: "yeni@test")));
        Assert.Equal("email_not_verified", ex.Error);

        // E-postadaki bağlantının token'ı olayın içinden alınır (gerçekte Worker e-postayla gönderir).
        var evt = f.Events.Published.Select(p => p.Event).OfType<StajDesk.Contracts.UserRegisteredEvent>().Single();
        var token = evt.VerifyUrl!.Split("token=")[1];
        await f.Auth.VerifyEmailAsync(token);
        Assert.NotNull((await f.Auth.AuthorizeAsync(f.Login(login: "yeni@test"))).Code);
        // Doğrulama bağlantısı şifre sıfırlamak için kullanılamaz.
        await Assert.ThrowsAsync<DomainExceptionAdapter>(() => f.Auth.ResetPasswordAsync(new ResetPasswordRequest(token, "Baska!Sifre2")));
    }

    [Fact]
    public async Task Invitation_link_sets_first_password_and_verifies_email()
    {
        var f = new Fixture();
        var invited = new User { FullName = "Davetli", Email = "davetli@test", PasswordHash = string.Empty };
        f.Db.Users.Add(invited);
        f.Db.SaveChanges();
        StajDesk.Application.Features.Account.AccountLinks.Invite(f.Db, f.Events, "http://app", invited, "İK");
        f.Db.SaveChanges();
        var token = f.Events.Published.Select(p => p.Event).OfType<StajDesk.Contracts.UserInvitedEvent>().Single().SetPasswordUrl.Split("token=")[1];

        // Şifresi belirlenmemiş hesapla giriş yapılamaz.
        await Assert.ThrowsAsync<OAuthException>(() => f.Auth.AuthorizeAsync(f.Login(login: "davetli@test", password: "")));
        await f.Auth.ResetPasswordAsync(new ResetPasswordRequest(token, "Yeni!Sifre123"));
        Assert.NotNull(f.Db.Users.Single(u => u.Email == "davetli@test").EmailVerifiedAt);
        Assert.NotNull((await f.Auth.AuthorizeAsync(f.Login(login: "davetli@test", password: "Yeni!Sifre123"))).Code);
        // Davet bağlantısı tek kullanımlık.
        await Assert.ThrowsAsync<DomainExceptionAdapter>(() => f.Auth.ResetPasswordAsync(new ResetPasswordRequest(token, "Baska!Sifre2")));
    }

    [Fact]
    public async Task Two_factor_login_requires_sms_code_when_provider_configured()
    {
        var f = new Fixture();
        f.Sms.Configured = true;
        f.Db.Users.Single().TwoFactorEnabled = true;
        f.Db.SaveChanges();

        var first = await f.Auth.AuthorizeAsync(f.Login());
        Assert.True(first.OtpRequired);
        Assert.Null(first.Code);
        Assert.DoesNotContain("1112233", first.MaskedPhone); // telefon maskeli

        var sentCode = System.Text.RegularExpressions.Regex.Match(f.Sms.Last!, @"\d{6}").Value;
        await Assert.ThrowsAsync<OAuthException>(() => f.Auth.VerifyLoginOtpAsync(new VerifyLoginOtpRequest(first.ChallengeId!.Value, sentCode == "000000" ? "000001" : "000000")));
        var done = await f.Auth.VerifyLoginOtpAsync(new VerifyLoginOtpRequest(first.ChallengeId!.Value, sentCode));
        Assert.NotNull(done.Code);
        // Kod tek kullanımlık.
        await Assert.ThrowsAsync<OAuthException>(() => f.Auth.VerifyLoginOtpAsync(new VerifyLoginOtpRequest(first.ChallengeId!.Value, sentCode)));
    }

    [Fact]
    public async Task Otp_locks_after_five_wrong_attempts()
    {
        var f = new Fixture();
        f.Sms.Configured = true;
        f.Db.Users.Single().TwoFactorEnabled = true;
        f.Db.SaveChanges();
        var first = await f.Auth.AuthorizeAsync(f.Login());
        var code = System.Text.RegularExpressions.Regex.Match(f.Sms.Last!, @"\d{6}").Value;
        var wrong = code == "123456" ? "654321" : "123456";
        for (var i = 0; i < OtpService.MaxAttempts; i++)
            await Assert.ThrowsAsync<OAuthException>(() => f.Auth.VerifyLoginOtpAsync(new VerifyLoginOtpRequest(first.ChallengeId!.Value, wrong)));
        // Doğru kod bile artık kabul edilmez.
        await Assert.ThrowsAsync<OAuthException>(() => f.Auth.VerifyLoginOtpAsync(new VerifyLoginOtpRequest(first.ChallengeId!.Value, code)));
    }

    [Fact]
    public async Task Without_sms_provider_two_factor_does_not_block_login()
    {
        var f = new Fixture(); // Sms.Configured = false (varsayılan)
        f.Db.Users.Single().TwoFactorEnabled = true;
        f.Db.SaveChanges();
        var res = await f.Auth.AuthorizeAsync(f.Login());
        Assert.False(res.OtpRequired);
        Assert.NotNull(res.Code);
        Assert.Null(f.Sms.Last); // sahte SMS gönderilmedi
    }

    // ---------- XSS ----------

    [Theory]
    [InlineData("<script>alert(1)</script>Merhaba", "alert(1)Merhaba")]
    [InlineData("<img src=x onerror=alert(1)>Yazıcı", "Yazıcı")]
    [InlineData("javascript:alert(1)", "alert(1)")]
    public void Text_clean_strips_markup(string input, string expected) => Assert.Equal(expected, Text.Clean(input));

    [Fact]
    public void Validator_rejects_html_in_ticket_title()
    {
        var result = new CreateTicketRequestValidator().Validate(new CreateTicketRequest("<b>Yazıcı</b> bozuk", "Açıklama metni yeterince uzun", 1));
        Assert.Contains(result.Errors, e => e.PropertyName == "Title");
    }

    // ---------- İzin matrisi ----------

    [Fact]
    public void Role_matrix_follows_least_privilege()
    {
        var user = RoleNames.Defaults.Single(r => r.Name == RoleNames.User).Permissions;
        var owner = RoleNames.Defaults.Single(r => r.Name == RoleNames.StoreOwner).Permissions;
        var admin = RoleNames.Defaults.Single(r => r.Name == RoleNames.SystemAdmin).Permissions;

        Assert.DoesNotContain(Permissions.TicketsReadAll, user);
        Assert.DoesNotContain(Permissions.TicketsManage, owner);
        Assert.Contains(Permissions.TicketsReadStore, owner);
        Assert.Equal(Permissions.All.Count, admin.Length);
        Assert.All(RoleNames.Defaults.SelectMany(r => r.Permissions), p => Assert.True(Permissions.IsKnown(p)));
    }
}
