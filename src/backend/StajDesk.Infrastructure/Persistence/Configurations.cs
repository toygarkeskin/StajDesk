using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StajDesk.Domain.Entities;

namespace StajDesk.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Email).IsUnique(); // giriş sorgusu e-postaya göre arar
        b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        b.Property(x => x.Username).HasMaxLength(50);
        b.HasIndex(x => x.Username).IsUnique().HasFilter("username IS NOT NULL");
        b.HasOne(x => x.Department).WithMany(d => d.Members).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Position).WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Manager).WithMany(m => m.DirectReports).HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.DepartmentId);
        b.HasIndex(x => x.ManagerId);
        b.ToTable(t => t.HasCheckConstraint("ck_users_not_own_manager", "manager_id IS NULL OR manager_id <> id"));
        b.Property(x => x.Phone).HasMaxLength(30);
        b.HasOne(x => x.Store).WithMany(s => s.Users).HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.StoreId);
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.Color).HasMaxLength(20).IsRequired();
    }
}

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(5000).IsRequired();
        b.Ignore(x => x.IsEditable);

        b.HasOne(x => x.Requester).WithMany(u => u.RequestedTickets)
            .HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Assignee).WithMany(u => u.AssignedTickets)
            .HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Category).WithMany(c => c.Tickets)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Asset).WithMany(a => a.Tickets)
            .HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Store).WithMany()
            .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.StoreId);

        // Sık filtrelenen/sıralanan sütunlara index
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.RequesterId);
        b.HasIndex(x => x.AssigneeId);
        b.HasIndex(x => x.CategoryId);
        b.HasIndex(x => x.CreatedAt);

        // PostgreSQL tam metin arama: başlık + açıklamadan Türkçe kök bulma (stemming) ile üretilen tsvector.
        // Sütun veritabanında hesaplanır (STORED); GIN index sayesinde aramada tüm tablo taranmaz.
        // Domain'i Npgsql tipine bağlamamak için gölge (shadow) özellik olarak tanımlandı.
        b.Property<NpgsqlTypes.NpgsqlTsVector>(TicketSearch.VectorProperty)
            .HasComputedColumnSql("to_tsvector('turkish', coalesce(title, '') || ' ' || coalesce(description, ''))", stored: true);
        b.HasIndex(TicketSearch.VectorProperty).HasMethod("GIN");
    }
}

internal sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> b)
    {
        b.Property(x => x.Body).HasMaxLength(5000).IsRequired();
        b.HasOne(x => x.Ticket).WithMany(t => t.Comments).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.TicketId);
    }
}

internal sealed class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> b)
    {
        b.ToTable("ticket_history");
        b.Property(x => x.OldValue).HasMaxLength(200);
        b.Property(x => x.NewValue).HasMaxLength(200);
        b.HasOne(x => x.Ticket).WithMany(t => t.History).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.TicketId);
    }
}

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.InventoryNumber).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.InventoryNumber).IsUnique();
        b.Property(x => x.SerialNumber).HasMaxLength(100);
        b.Property(x => x.Manufacturer).HasMaxLength(100);
        b.Property(x => x.Model).HasMaxLength(100);
        b.Property(x => x.Location).HasMaxLength(120);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasOne(x => x.AssignedUser).WithMany(u => u.Assets)
            .HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Store).WithMany()
            .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.StoreId);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Ticket).WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        // "Okunmamış bildirimlerim" sorgusu için bileşik index
        b.HasIndex(x => new { x.UserId, x.IsRead });
    }
}

// ---------- Mağaza, rol ve izin (RBAC) ----------

internal sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> b)
    {
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.City).HasMaxLength(80);
        b.Property(x => x.Address).HasMaxLength(250);
        b.Property(x => x.Phone).HasMaxLength(30);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.Property(x => x.Key).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Key).IsUnique();
        b.Property(x => x.Group).HasMaxLength(50).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
    }
}

/// <summary>Rol - İzin (çoka-çok). Birleşik birincil anahtar.</summary>
internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.HasKey(x => new { x.RoleId, x.PermissionId });
        b.HasOne(x => x.Role).WithMany(r => r.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Kullanıcı - Rol (çoka-çok).</summary>
internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.HasKey(x => new { x.UserId, x.RoleId });
        b.HasOne(x => x.User).WithMany(u => u.Roles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

// ---------- OAuth 2.0 ----------

internal sealed class OAuthClientConfiguration : IEntityTypeConfiguration<OAuthClient>
{
    public void Configure(EntityTypeBuilder<OAuthClient> b)
    {
        b.ToTable("oauth_clients");
        b.HasKey(x => x.ClientId);
        b.Property(x => x.ClientId).HasMaxLength(80);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.SecretHash).HasMaxLength(500);
    }
}

internal sealed class AuthorizationCodeConfiguration : IEntityTypeConfiguration<AuthorizationCode>
{
    public void Configure(EntityTypeBuilder<AuthorizationCode> b)
    {
        b.ToTable("oauth_authorization_codes");
        b.HasKey(x => x.CodeHash);
        b.Property(x => x.CodeHash).HasMaxLength(64);
        b.Property(x => x.ClientId).HasMaxLength(80).IsRequired();
        b.Property(x => x.CodeChallenge).HasMaxLength(128).IsRequired();
        b.Property(x => x.CodeChallengeMethod).HasMaxLength(10).IsRequired();
        b.HasOne<OAuthClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("oauth_refresh_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.FamilyId);
        b.Property(x => x.ClientId).HasMaxLength(80).IsRequired();
        b.Property(x => x.UserAgent).HasMaxLength(300);
        b.Property(x => x.Ip).HasMaxLength(64);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<OAuthClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.HasKey(x => x.TokenHash);
        b.Property(x => x.TokenHash).HasMaxLength(64);
        b.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(30);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

// ---------- Outbox, e-posta, denetim ----------

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.Property(x => x.RoutingKey).HasMaxLength(80).IsRequired();
        b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.LastError).HasMaxLength(500);
        // Yalnızca yayınlanmamış mesajlar için kısmi index (dağıtıcı sorgusu)
        b.HasIndex(x => x.OccurredAt).HasFilter("published_at IS NULL");
    }
}

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> b) => b.HasKey(x => x.MessageId);
}

internal sealed class SentEmailConfiguration : IEntityTypeConfiguration<SentEmail>
{
    public void Configure(EntityTypeBuilder<SentEmail> b)
    {
        b.Property(x => x.ToAddress).HasMaxLength(150).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(250).IsRequired();
        b.Property(x => x.Template).HasMaxLength(60).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.Error).HasMaxLength(500);
        b.HasIndex(x => x.CreatedAt);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.Action).HasMaxLength(80).IsRequired();
        b.Property(x => x.Entity).HasMaxLength(50).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.Data).HasColumnType("jsonb");
        b.Property(x => x.Ip).HasMaxLength(64);
        b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.CreatedAt);
    }
}

// ---------- Panel: duyurular, nöbet çizelgesi; talep ekleri ----------

internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> b)
    {
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Level).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        // Panel sorgusu: yayında olan duyurular, en yenisi önce
        b.HasIndex(x => new { x.PublishAt, x.ExpiresAt });
    }
}

internal sealed class OnCallShiftConfiguration : IEntityTypeConfiguration<OnCallShift>
{
    public void Configure(EntityTypeBuilder<OnCallShift> b)
    {
        b.Property(x => x.Note).HasMaxLength(200);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.StartsOn, x.EndsOn });
        b.ToTable(t => t.HasCheckConstraint("ck_on_call_shifts_range", "ends_on >= starts_on"));
    }
}

internal sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> b)
    {
        b.Property(x => x.FileName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.StorageKey).IsUnique();
        b.HasOne(x => x.Ticket).WithMany(t => t.Attachments).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_ticket_attachments_size", "size > 0"));
    }
}

// ---------- Organizasyon: departman, unvan, personel kartı, tercihler, OTP ----------

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        // Alt departmanı olan departman silinemez (ağaç kopmasın).
        b.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.ParentId);
        b.ToTable(t => t.HasCheckConstraint("ck_departments_not_own_parent", "parent_id IS NULL OR parent_id <> id"));
    }
}

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> b)
    {
        b.Property(x => x.Title).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Title).IsUnique();
        b.Property(x => x.Description).HasMaxLength(300);
        b.ToTable(t => t.HasCheckConstraint("ck_positions_level", "level BETWEEN 1 AND 20"));
    }
}

internal sealed class EmployeeProfileConfiguration : IEntityTypeConfiguration<EmployeeProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeProfile> b)
    {
        b.HasKey(x => x.UserId);
        b.HasOne(x => x.User).WithOne(u => u.Profile).HasForeignKey<EmployeeProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.EmployeeNumber).HasMaxLength(20);
        b.HasIndex(x => x.EmployeeNumber).IsUnique().HasFilter("employee_number IS NOT NULL");
        b.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PersonalEmail).HasMaxLength(150);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.EmergencyContactName).HasMaxLength(100);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(30);
        b.Property(x => x.EmergencyContactRelation).HasMaxLength(50);
        b.Property(x => x.PhotoKey).HasMaxLength(40);
        b.Property(x => x.CoverKey).HasMaxLength(40);
        b.ToTable(t => t.HasCheckConstraint("ck_employee_profiles_dates", "termination_date IS NULL OR hire_date IS NULL OR termination_date >= hire_date"));
    }
}

internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> b)
    {
        b.HasKey(x => x.UserId);
        b.HasOne(x => x.User).WithOne(u => u.Settings).HasForeignKey<UserSettings>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Language).HasMaxLength(5);
        b.Property(x => x.Theme).HasMaxLength(10);
    }
}

internal sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> b)
    {
        b.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Payload).HasMaxLength(2000);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.UserId);
    }
}

internal sealed class AccountRequestConfiguration : IEntityTypeConfiguration<AccountRequest>
{
    public void Configure(EntityTypeBuilder<AccountRequest> b)
    {
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.EmployeeNumber).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.ReviewNote).HasMaxLength(1000);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Position).WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.TargetUser).WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Cascade);
        // Talep geçmişi silinmesin diye talep açan/inceleyen kişi silinemez.
        b.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        // Aynı e-posta için aynı anda tek bekleyen yeni hesap talebi
        b.HasIndex(x => x.Email).IsUnique().HasFilter("status = 'Pending' AND type = 'NewAccount'");
        b.ToTable(t => t.HasCheckConstraint("ck_account_requests_shape",
            "(type = 'NewAccount' AND email IS NOT NULL) OR (type = 'Deactivation' AND target_user_id IS NOT NULL)"));
    }
}

// ---------- Kurumsal site: haber, reklam, kariyer, ayarlar, medya, dış veri ----------

internal sealed class NewsArticleConfiguration : IEntityTypeConfiguration<NewsArticle>
{
    public void Configure(EntityTypeBuilder<NewsArticle> b)
    {
        b.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(400).IsRequired();
        b.Property(x => x.Body).HasMaxLength(20000).IsRequired();
        b.Property(x => x.ImageKey).HasMaxLength(40);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.IsPublished, x.PublishedAt });
    }
}

internal sealed class AdvertisementConfiguration : IEntityTypeConfiguration<Advertisement>
{
    public void Configure(EntityTypeBuilder<Advertisement> b)
    {
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.ImageKey).HasMaxLength(40).IsRequired();
        b.Property(x => x.LinkUrl).HasMaxLength(500);
        b.Property(x => x.Page).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Placement).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.Page, x.Placement, x.IsActive });
        b.ToTable(t => t.HasCheckConstraint("ck_advertisements_dates", "ends_at IS NULL OR ends_at > starts_at"));
    }
}

internal sealed class AdStatDailyConfiguration : IEntityTypeConfiguration<AdStatDaily>
{
    public void Configure(EntityTypeBuilder<AdStatDaily> b)
    {
        b.ToTable("ad_stats_daily");
        b.HasKey(x => new { x.AdvertisementId, x.Day });
        b.HasOne(x => x.Advertisement).WithMany(a => a.Stats).HasForeignKey(x => x.AdvertisementId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class JobOpeningConfiguration : IEntityTypeConfiguration<JobOpening>
{
    public void Configure(EntityTypeBuilder<JobOpening> b)
    {
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Location).HasMaxLength(100).IsRequired();
        b.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Description).HasMaxLength(5000).IsRequired();
        b.Property(x => x.Requirements).HasMaxLength(5000);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.IsPublished, x.PublishedAt });
    }
}

internal sealed class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> b)
    {
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(60);
        b.Property(x => x.Value).HasMaxLength(10000);
    }
}

internal sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> b)
    {
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(40);
        b.Property(x => x.ContentType).HasMaxLength(50);
        b.Property(x => x.FileName).HasMaxLength(200);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ExternalSnapshotConfiguration : IEntityTypeConfiguration<ExternalSnapshot>
{
    public void Configure(EntityTypeBuilder<ExternalSnapshot> b)
    {
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(60);
        b.Property(x => x.Source).HasMaxLength(100);
        b.Property(x => x.Payload).HasMaxLength(20000);
    }
}
