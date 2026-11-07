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
        b.Ignore(x => x.IsStaff);
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
