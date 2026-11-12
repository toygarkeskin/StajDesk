using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StajDesk.Api.Infrastructure;
using StajDesk.Application.Features.Auth;
using StajDesk.Infrastructure.Persistence;

namespace StajDesk.Api.Controllers;

/// <summary>
/// OAuth 2.0 yetkilendirme sunucusu uç noktaları.
///  POST /connect/authorize  — kimlik bilgileri + PKCE → yetkilendirme kodu (tarayıcı uygulamasının giriş formu bu ucu çağırır)
///  POST /connect/token      — authorization_code | refresh_token | client_credentials (form veya JSON)
///  POST /connect/revoke     — RFC 7009
///  GET  /.well-known/oauth-authorization-server — RFC 8414 meta verisi
///  GET  /.well-known/jwks.json — açık imza anahtarı
/// Yanıt hataları RFC 6749 §5.2 biçimindedir: { "error": "...", "error_description": "..." }.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class OAuthController(IAuthService auth, SigningKeyProvider keys, IOptions<JwtOptions> jwt, IConfiguration config) : ControllerBase
{
    /// <summary>İlk taraf (first-party) tarayıcı uygulamasının refresh token'ı JavaScript'in erişemeyeceği httpOnly cookie'de tutulur (XSS'e karşı).</summary>
    public const string RefreshCookie = "sd_rt";

    [HttpPost("/connect/authorize")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthorizeResponse>> Authorize([FromBody] AuthorizeRequest request, CancellationToken ct) =>
        Ok(await auth.AuthorizeAsync(request, ct));

    /// <summary>İki adımlı girişin ikinci adımı: SMS kodu doğrulanınca yetki kodu verilir.</summary>
    [HttpPost("/connect/authorize/otp")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthorizeResponse>> AuthorizeOtp([FromBody] VerifyLoginOtpRequest request, CancellationToken ct) =>
        Ok(await auth.VerifyLoginOtpAsync(request, ct));

    [HttpPost("/connect/token")]
    [EnableRateLimiting("auth")]
    [Consumes("application/x-www-form-urlencoded", "application/json")]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        var form = await ReadParametersAsync(ct);
        string? P(string name) => form.TryGetValue(name, out var v) && !string.IsNullOrEmpty(v) ? v : null;

        // İstemci kimlik doğrulaması: client_secret_basic (Authorization: Basic) veya client_secret_post
        var (clientId, clientSecret) = ReadBasicAuth() ?? (P("client_id") ?? "", P("client_secret"));
        var grantType = P("grant_type") ?? "";
        var refresh = P("refresh_token");
        var fromCookie = false;
        if (grantType == "refresh_token" && refresh is null && Request.Cookies.TryGetValue(RefreshCookie, out var cookie))
        {
            refresh = cookie;
            fromCookie = true;
        }

        var result = await auth.TokenAsync(new TokenRequest(grantType, P("code"), P("code_verifier"), P("redirect_uri"), refresh, P("scope"), clientId, clientSecret),
            Request.Headers.UserAgent.ToString(), ct);

        Response.Headers.CacheControl = "no-store"; // RFC 6749 §5.1
        Response.Headers.Pragma = "no-cache";
        var body = new Dictionary<string, object>
        {
            ["access_token"] = result.AccessToken,
            ["token_type"] = result.TokenType,
            ["expires_in"] = result.ExpiresIn,
            ["scope"] = result.Scope,
        };
        if (result.RefreshToken is not null)
        {
            if (result.FirstParty)
            {
                Response.Cookies.Append(RefreshCookie, result.RefreshToken, new CookieOptions
                {
                    HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = config.GetValue("OAuth:SecureCookies", false),
                    Path = "/connect", Expires = DateTimeOffset.UtcNow.AddDays(config.GetValue("OAuth:RefreshTokenDays", 14)),
                });
            }
            else body["refresh_token"] = result.RefreshToken;
        }
        _ = fromCookie;
        return Ok(body);
    }

    [HttpPost("/connect/revoke")]
    public async Task<IActionResult> Revoke(CancellationToken ct)
    {
        var form = await ReadParametersAsync(ct);
        var token = form.GetValueOrDefault("token");
        var clientId = ReadBasicAuth()?.ClientId ?? form.GetValueOrDefault("client_id") ?? DbSeeder.WebClientId;
        if (string.IsNullOrEmpty(token) && Request.Cookies.TryGetValue(RefreshCookie, out var cookie)) token = cookie;
        await auth.RevokeAsync(token, clientId, ct);
        Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = "/connect" });
        return Ok();
    }

    [HttpGet("/.well-known/oauth-authorization-server")]
    public IActionResult Metadata()
    {
        var issuer = jwt.Value.Issuer.TrimEnd('/');
        return Ok(new Dictionary<string, object>
        {
            ["issuer"] = issuer,
            ["authorization_endpoint"] = $"{config["OAuth:WebUrl"]?.TrimEnd('/') ?? issuer}/oauth/authorize",
            ["token_endpoint"] = $"{issuer}/connect/token",
            ["revocation_endpoint"] = $"{issuer}/connect/revoke",
            ["jwks_uri"] = $"{issuer}/.well-known/jwks.json",
            ["response_types_supported"] = new[] { "code" },
            ["grant_types_supported"] = new[] { "authorization_code", "refresh_token", "client_credentials" },
            ["code_challenge_methods_supported"] = new[] { "S256" },
            ["token_endpoint_auth_methods_supported"] = new[] { "none", "client_secret_basic", "client_secret_post" },
            ["scopes_supported"] = new[] { "openid", "profile", "api" },
        });
    }

    [HttpGet("/.well-known/jwks.json")]
    public IActionResult Jwks() => Ok(new { keys = new[] { new { kty = keys.Jwk.Kty, use = keys.Jwk.Use, alg = keys.Jwk.Alg, kid = keys.Jwk.Kid, n = keys.Jwk.N, e = keys.Jwk.E } } });

    private async Task<Dictionary<string, string?>> ReadParametersAsync(CancellationToken ct)
    {
        if (Request.HasFormContentType)
            return (await Request.ReadFormAsync(ct)).ToDictionary(k => k.Key, v => (string?)v.Value.ToString());
        var json = await Request.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>(ct) ?? [];
        return json.ToDictionary(k => k.Key, v => v.Value.ValueKind == System.Text.Json.JsonValueKind.String ? v.Value.GetString() : v.Value.ToString());
    }

    private (string ClientId, string? Secret)? ReadBasicAuth()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
            var idx = decoded.IndexOf(':');
            return idx < 0 ? null : (Uri.UnescapeDataString(decoded[..idx]), Uri.UnescapeDataString(decoded[(idx + 1)..]));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
