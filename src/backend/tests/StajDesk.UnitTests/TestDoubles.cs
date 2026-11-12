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
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<OnCallShift> OnCallShifts => Set<OnCallShift>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<AccountRequest> AccountRequests => Set<AccountRequest>();
    public DbSet<NewsArticle> News => Set<NewsArticle>();
    public DbSet<Advertisement> Advertisements => Set<Advertisement>();
    public DbSet<AdStatDaily> AdStats => Set<AdStatDaily>();
    public DbSet<JobOpening> JobOpenings => Set<JobOpening>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<ExternalSnapshot> ExternalSnapshots => Set<ExternalSnapshot>();
    public DbSet<DashboardWidgetConfig> DashboardWidgets => Set<DashboardWidgetConfig>();
    public DbSet<UserDashboardPreference> DashboardPreferences => Set<UserDashboardPreference>();
    public DbSet<CompanyEvent> CompanyEvents => Set<CompanyEvent>();
    public DbSet<Recognition> Recognitions => Set<Recognition>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStepDefinition> WorkflowStepDefinitions => Set<WorkflowStepDefinition>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowApproval> WorkflowApprovals => Set<WorkflowApproval>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveEntitlement> LeaveEntitlements => Set<LeaveEntitlement>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<AnnouncementRead> AnnouncementReads => Set<AnnouncementRead>();
    public DbSet<SocialPost> SocialPosts => Set<SocialPost>();
    public DbSet<SocialComment> SocialComments => Set<SocialComment>();
    public DbSet<SocialLike> SocialLikes => Set<SocialLike>();
    public DbSet<SocialReport> SocialReports => Set<SocialReport>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();

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
        b.Entity<AccountRequest>().HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById);
        b.Entity<AccountRequest>().HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById);
        b.Entity<AccountRequest>().HasOne(x => x.TargetUser).WithMany().HasForeignKey(x => x.TargetUserId);
        b.Entity<AccountRequest>().HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId);
        b.Entity<AdStatDaily>().HasKey(x => new { x.AdvertisementId, x.Day });
        b.Entity<SiteSetting>().HasKey(x => x.Key);
        b.Entity<MediaFile>().HasKey(x => x.Key);
        b.Entity<ExternalSnapshot>().HasKey(x => x.Key);
        b.Entity<DashboardWidgetConfig>().HasKey(x => x.Key);
        b.Entity<UserDashboardPreference>().HasKey(x => new { x.UserId, x.WidgetKey });
        b.Entity<Recognition>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        b.Entity<LeaveEntitlement>().HasKey(x => new { x.UserId, x.LeaveTypeId, x.Year });
        b.Entity<WorkflowStepDefinition>().HasOne<WorkflowDefinition>().WithMany(d => d.Steps).HasForeignKey(x => x.WorkflowDefinitionId);
        b.Entity<WorkflowInstance>().HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId);
        b.Entity<WorkflowApproval>().HasOne(x => x.Instance).WithMany(i => i.Approvals).HasForeignKey(x => x.WorkflowInstanceId);
        b.Entity<WorkflowApproval>().HasOne(x => x.ApproverUser).WithMany().HasForeignKey(x => x.ApproverUserId);
        b.Entity<WorkflowApproval>().HasOne(x => x.DecidedBy).WithMany().HasForeignKey(x => x.DecidedById);
        b.Entity<LeaveRequest>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        b.Entity<AnnouncementRead>().HasKey(x => new { x.AnnouncementId, x.UserId });
        b.Entity<AnnouncementRead>().HasOne<Announcement>().WithMany(a => a.Reads).HasForeignKey(x => x.AnnouncementId);
        b.Entity<AnnouncementRead>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        b.Entity<WorkTask>().HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId);
        b.Entity<WorkTask>().HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById);
        b.Entity<Announcement>().HasOne(x => x.TargetDepartment).WithMany().HasForeignKey(x => x.TargetDepartmentId);
        b.Entity<SocialLike>().HasKey(x => new { x.PostId, x.UserId });
        b.Entity<SocialLike>().HasOne<SocialPost>().WithMany(p => p.Likes).HasForeignKey(x => x.PostId);
        b.Entity<SocialPost>().HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId);
        b.Entity<SocialComment>().HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId);
        b.Entity<SocialReport>().HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId);
        b.Entity<Conversation>().HasOne(x => x.UserA).WithMany().HasForeignKey(x => x.UserAId);
        b.Entity<Conversation>().HasOne(x => x.UserB).WithMany().HasForeignKey(x => x.UserBId);
        b.Entity<EmployeeProfile>().HasKey(x => x.UserId);
        b.Entity<EmployeeProfile>().HasOne(x => x.User).WithOne(u => u.Profile).HasForeignKey<EmployeeProfile>(x => x.UserId);
        b.Entity<UserSettings>().HasKey(x => x.UserId);
        b.Entity<UserSettings>().HasOne(x => x.User).WithOne(u => u.Settings).HasForeignKey<UserSettings>(x => x.UserId);
        b.Entity<User>().HasOne(x => x.Manager).WithMany(m => m.DirectReports).HasForeignKey(x => x.ManagerId);
        b.Entity<User>().HasOne(x => x.Department).WithMany(d => d.Members).HasForeignKey(x => x.DepartmentId);
        b.Entity<Department>().HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId);
        b.Entity<Department>().HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId);
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

/// <summary>Testte SMS: yapılandırılmış/yapılandırılmamış durumlarını taklit eder, gönderilen son mesajı saklar.</summary>
internal sealed class FakeSms : ISmsSender
{
    public bool Configured { get; set; }
    public string? Last { get; private set; }
    public bool IsConfigured => Configured;
    public string ProviderName => "test";
    public Task SendAsync(string phone, string message, CancellationToken ct = default) { Last = message; return Task.CompletedTask; }
}
