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
        b.Property(x => x.Department).HasMaxLength(100);
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
