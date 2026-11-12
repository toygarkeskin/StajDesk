using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Security;
using StajDesk.Infrastructure.Persistence;

namespace StajDesk.Api.Infrastructure;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "http://localhost:5080";
    public string Audience { get; set; } = "stajdesk-api";
    public int ExpiresMinutes { get; set; } = 15;
    /// <summary>RS256 anahtar çiftinin saklandığı klasör (Docker'da volume). Yoksa ilk açılışta üretilir.</summary>
    public string KeysDirectory { get; set; } = ".keys";
}

/// <summary>
/// RS256 imza anahtarı. Özel anahtar yalnızca sunucuda; açık anahtar /.well-known/jwks.json'da yayınlanır,
/// böylece başka servisler token'ı imzasından doğrulayabilir. Anahtar dosyada kalıcıdır (yeniden başlatmada oturumlar düşmez).
/// </summary>
public sealed class SigningKeyProvider
{
    public RsaSecurityKey PrivateKey { get; }
    public RsaSecurityKey PublicKey { get; }
    public JsonWebKey Jwk { get; }

    public SigningKeyProvider(IOptions<JwtOptions> options, ILogger<SigningKeyProvider> logger)
    {
        var dir = options.Value.KeysDirectory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "jwt-signing-key.pem");
        var rsa = RSA.Create();
        if (File.Exists(path)) rsa.ImportFromPem(File.ReadAllText(path));
        else
        {
            rsa.KeySize = 2048;
            File.WriteAllText(path, rsa.ExportPkcs8PrivateKeyPem());
            logger.LogInformation("Yeni JWT imza anahtarı üretildi: {Path}", path);
        }

        var kid = Base64UrlEncoder.Encode(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))[..16];
        PrivateKey = new RsaSecurityKey(rsa) { KeyId = kid };
        var pub = RSA.Create();
        pub.ImportParameters(rsa.ExportParameters(false));
        PublicKey = new RsaSecurityKey(pub) { KeyId = kid };
        Jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(PublicKey);
        Jwk.Use = "sig";
        Jwk.Alg = SecurityAlgorithms.RsaSha256;
    }
}

internal sealed class JwtAccessTokenIssuer(SigningKeyProvider keys, IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    public int ExpiresInSeconds => options.Value.ExpiresMinutes * 60;

    public string Issue(AccessTokenClaims c)
    {
        var o = options.Value;
        // Payload herkes tarafından okunabilir (base64) → şifre gibi gizli bilgi koymuyoruz.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("client_id", c.ClientId),
            new("scope", c.Scope),
        };
        if (c.UserId is { } uid) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, uid.ToString()));
        if (c.Name is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Name, c.Name));
        if (c.SessionId is { } sid) claims.Add(new Claim("sid", sid.ToString()));
        claims.AddRange(c.Roles.Select(r => new Claim("role", r)));

        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, expires: DateTime.UtcNow.AddSeconds(ExpiresInSeconds),
            signingCredentials: new SigningCredentials(keys.PrivateKey, SecurityAlgorithms.RsaSha256));
        token.Header["typ"] = "at+jwt";
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>Kullanıcının izinleri ve mağazası (Redis'te 5 dk önbelleklenir; rol değişince silinir).</summary>
public sealed record UserAccess(int StoreId, bool IsActive, string[] Permissions)
{
    public int? Store => StoreId == 0 ? null : StoreId;
}

/// <summary>
/// Kimlik doğrulamadan sonra çalışır: token'daki kullanıcının izinlerini veritabanı/Redis'ten yükler.
/// İzinler token'a gömülmez → rol değişikliği veya hesabın pasifleştirilmesi hemen etkili olur.
/// </summary>
public sealed class PermissionLoadingMiddleware(RequestDelegate next)
{
    public const string ItemKey = "stajdesk.access";

    public async Task InvokeAsync(HttpContext ctx, AppDbContext db, ICacheService cache)
    {
        var principal = ctx.User;
        if (principal.Identity?.IsAuthenticated == true)
        {
            if (int.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
            {
                var access = await cache.GetOrSetAsync(CacheKeys.Permissions(userId), async ct =>
                {
                    // Kilitli hesap pasif hesap gibi davranır: token geçerli olsa bile istek reddedilir.
                    var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { IsActive = u.IsActive && u.LockedAt == null, u.StoreId }).FirstOrDefaultAsync(ct);
                    if (user is null) return new UserAccess(0, false, []);
                    var perms = await db.UserRoles.Where(ur => ur.UserId == userId)
                        .SelectMany(ur => ur.Role.Permissions.Select(p => p.Permission.Key)).Distinct().ToArrayAsync(ct);
                    return new UserAccess(user.StoreId ?? 0, user.IsActive, perms);
                }, TimeSpan.FromMinutes(5), ctx.RequestAborted);

                if (!access.IsActive)
                {
                    // Pasif hesap: token geçerli olsa bile istek reddedilir.
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsJsonAsync(ApiResponse.Fail("Hesabınız pasif veya kilitli. Bilgi Teknolojileri ile iletişime geçin."));
                    return;
                }
                ctx.Items[ItemKey] = access;
            }
            else
            {
                // client_credentials: kullanıcısız token; izinler = token kapsamı (yalnızca bilinen izin anahtarları).
                var scopes = (principal.FindFirstValue("scope") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(Permissions.IsKnown).ToArray();
                ctx.Items[ItemKey] = new UserAccess(0, true, scopes);
            }
        }
        await next(ctx);
    }
}

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Ctx => accessor.HttpContext;
    private ClaimsPrincipal? Principal => Ctx?.User;
    private UserAccess? Access => Ctx?.Items[PermissionLoadingMiddleware.ItemKey] as UserAccess;

    public int? UserId => int.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
    public string? FullName => Principal?.FindFirstValue(JwtRegisteredClaimNames.Name);
    public int? StoreId => Access?.Store;
    public IReadOnlyCollection<string> Roles => Principal?.FindAll("role").Select(c => c.Value).ToList() ?? [];
    public IReadOnlySet<string> Permissions => Access is null ? new HashSet<string>() : new HashSet<string>(Access.Permissions);
    public string? ClientId => UserId is null && Principal?.Identity?.IsAuthenticated == true ? Principal.FindFirstValue("client_id") : null;
    public string? IpAddress => Ctx?.Connection.RemoteIpAddress?.ToString();
}

/// <summary>
/// Controller/aksiyon üzerinde izin şartı (derinlemesine savunma; servisler de ayrıca kontrol eder).
/// Örnek: <c>[RequirePermission(Permissions.UsersManage)]</c> — listedeki izinlerden biri yeterlidir.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(params string[] anyOf) : Attribute, IAuthorizationFilter
{
    public string[] AnyOf { get; } = anyOf;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return; // [Authorize] 401'i üretir
        var access = context.HttpContext.Items[PermissionLoadingMiddleware.ItemKey] as UserAccess;
        if (access is null || !AnyOf.Any(p => access.Permissions.Contains(p)))
            context.Result = new ObjectResult(ApiResponse.Fail("Bu işlem rolünüze açık değil. Erişim gerekiyorsa sistem yöneticinizden isteyin.")) { StatusCode = 403 };
    }
}
