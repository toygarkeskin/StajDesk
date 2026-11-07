namespace StajDesk.Domain.Entities;

/// <summary>OAuth 2.0 istemcisi (RFC 6749 §2). Tarayıcı uygulaması "public client" (gizli anahtarı yok), servisler "confidential client".</summary>
public class OAuthClient
{
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Gizli istemciler için hash; public client için null.</summary>
    public string? SecretHash { get; set; }
    public List<string> RedirectUris { get; set; } = [];
    public List<string> GrantTypes { get; set; } = [];
    /// <summary>client_credentials ile verilebilecek izinler (kapsam = izin anahtarı).</summary>
    public List<string> Scopes { get; set; } = [];
    public bool IsFirstParty { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Tek kullanımlık, 60 sn ömürlü yetkilendirme kodu (yalnızca SHA-256 özeti saklanır).</summary>
public class AuthorizationCode
{
    public string CodeHash { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string RedirectUri { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string CodeChallengeMethod { get; set; } = "S256";
    public string Scope { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}

/// <summary>Opak refresh token. Her kullanımda döndürülür (rotation); eski token tekrar gelirse tüm aile iptal edilir.</summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TokenHash { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string ClientId { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedBy { get; set; }
    public string? UserAgent { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PasswordResetToken
{
    public string TokenHash { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
