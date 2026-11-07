using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StajDesk.Application.Abstractions;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.Api.Infrastructure;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "stajdesk";
    public string Audience { get; set; } = "stajdesk-web";
    /// <summary>En az 32 karakter. Asla koda yazılmaz; .env veya user-secrets'tan gelir.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 480;
}

public static class Roles
{
    public const string User = nameof(UserRole.User);
    public const string Agent = nameof(UserRole.Agent);
    public const string Admin = nameof(UserRole.Admin);
    public const string Staff = Agent + "," + Admin;
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public const string RoleClaim = "role";

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var o = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(o.ExpiresMinutes);
        // Payload herkes tarafından okunabilir (base64) → şifre gibi gizli bilgi koymuyoruz.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim(RoleClaim, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public int? UserId =>
        int.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id : null;

    public string? FullName => Principal?.FindFirstValue(JwtRegisteredClaimNames.Name);

    public UserRole? Role => Enum.TryParse<UserRole>(Principal?.FindFirstValue(JwtTokenService.RoleClaim), out var r) ? r : null;
}
