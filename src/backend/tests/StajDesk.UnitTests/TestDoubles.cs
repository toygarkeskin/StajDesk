using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Contracts;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>Testler için bellek içi veritabanı (gerçek PostgreSQL gerekmez).</summary>
internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();
    public DbSet<AuthorizationCode> AuthorizationCodes => Set<AuthorizationCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Ticket>().Ignore(t => t.IsEditable);
        b.Entity<Ticket>().HasOne(t => t.Requester).WithMany(u => u.RequestedTickets).HasForeignKey(t => t.RequesterId);
        b.Entity<Ticket>().HasOne(t => t.Assignee).WithMany(u => u.AssignedTickets).HasForeignKey(t => t.AssigneeId);
        b.Entity<Ticket>().HasOne(t => t.Store).WithMany().HasForeignKey(t => t.StoreId);
        b.Entity<Asset>().HasOne(a => a.Store).WithMany().HasForeignKey(a => a.StoreId);
        b.Entity<TicketComment>().HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId);
        b.Entity<TicketHistory>().HasOne(h => h.Actor).WithMany().HasForeignKey(h => h.ActorId);
        b.Entity<Notification>().HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId);
        b.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
        b.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        b.Entity<UserRole>().HasOne(x => x.User).WithMany(u => u.Roles).HasForeignKey(x => x.UserId);
        b.Entity<OAuthClient>().HasKey(x => x.ClientId);
        b.Entity<AuthorizationCode>().HasKey(x => x.CodeHash);
        b.Entity<PasswordResetToken>().HasKey(x => x.TokenHash);
        b.Entity<AuditLog>().HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId);
    }

    public static TestDbContext Create() =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Varsayılan rol/izin matrisini yükler (gerçek seed ile aynı tanımlar).</summary>
    public Dictionary<string, Role> SeedRoles()
    {
        var perms = StajDesk.Domain.Security.Permissions.All.Select(p => new Permission { Key = p.Key, Group = p.Group, Description = p.Description }).ToList();
        Permissions.AddRange(perms);
        SaveChanges();
        var roles = new Dictionary<string, Role>();
        foreach (var def in RoleNames.Defaults)
        {
            var role = new Role { Name = def.Name, DisplayName = def.DisplayName };
            foreach (var key in def.Permissions) role.Permissions.Add(new RolePermission { Permission = perms.First(p => p.Key == key) });
            Roles.Add(role);
            roles[def.Name] = role;
        }
        SaveChanges();
        return roles;
    }
}

/// <summary>Test kullanıcısı: izinleri gerçek rol matrisinden alır.</summary>
internal sealed class FakeCurrentUser : ICurrentUser
{
    public int? UserId { get; set; }
    public string? FullName { get; set; } = "Test";
    public int? StoreId { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();
    public string? ClientId { get; set; }
    public string? IpAddress { get; set; } = "127.0.0.1";

    public void As(User u, string role)
    {
        UserId = u.Id;
        FullName = u.FullName;
        StoreId = u.StoreId;
        Roles = [role];
        Permissions = new HashSet<string>(RoleNames.Defaults.First(r => r.Name == role).Permissions);
    }
}

internal sealed class FakeEventPublisher : IEventPublisher
{
    public List<(IntegrationEvent Event, string RoutingKey)> Published { get; } = [];
    public void Enqueue<T>(T @event, string routingKey) where T : IntegrationEvent => Published.Add((@event, routingKey));
}

internal sealed class NoCache : ICacheService
{
    public int Invalidations { get; private set; }
    public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default) => factory(ct);
    public Task RemoveAsync(string key, CancellationToken ct = default) { Invalidations++; return Task.CompletedTask; }
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) { Invalidations++; return Task.CompletedTask; }
}

/// <summary>Bellek içi veritabanı transaction desteklemez; işi doğrudan çalıştırır.</summary>
internal sealed class DirectUnitOfWork : IUnitOfWork
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default) => work(ct);
}

internal sealed class NoAudit : IAuditLogger
{
    public List<string> Actions { get; } = [];
    public void Add(string action, string entity, object? entityId, object? data = null) => Actions.Add(action);
}

internal sealed class InMemoryRateLimiter(int max = 5) : IRateLimiter
{
    private readonly Dictionary<string, int> _hits = [];
    public Task<int?> GetBlockedSecondsAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(_hits.GetValueOrDefault(key) >= max ? (int?)300 : null);
    public Task RegisterHitAsync(string key, CancellationToken ct = default) { _hits[key] = _hits.GetValueOrDefault(key) + 1; return Task.CompletedTask; }
    public Task ResetAsync(string key, CancellationToken ct = default) { _hits.Remove(key); return Task.CompletedTask; }
}

internal sealed class FakeTokenIssuer : IAccessTokenIssuer
{
    public int ExpiresInSeconds => 900;
    public AccessTokenClaims? Last { get; private set; }
    public string Issue(AccessTokenClaims claims) { Last = claims; return $"token-{claims.UserId}-{claims.ClientId}"; }
}

/// <summary>Testte hız için düz SHA-256 (gerçek uygulama PBKDF2 kullanır).</summary>
internal sealed class FakeHasher : IPasswordHasher
{
    public string Hash(string password) => "h:" + password;
    public bool Verify(string hash, string password) => hash == "h:" + password;
}
