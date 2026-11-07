using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Contracts;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.UnitTests;

/// <summary>Testler için bellek içi veritabanı (gerçek PostgreSQL gerekmez).</summary>
internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().Ignore(u => u.IsStaff);
        b.Entity<Ticket>().Ignore(t => t.IsEditable);
        b.Entity<Ticket>().HasOne(t => t.Requester).WithMany(u => u.RequestedTickets).HasForeignKey(t => t.RequesterId);
        b.Entity<Ticket>().HasOne(t => t.Assignee).WithMany(u => u.AssignedTickets).HasForeignKey(t => t.AssigneeId);
        b.Entity<TicketComment>().HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId);
        b.Entity<TicketHistory>().HasOne(h => h.Actor).WithMany().HasForeignKey(h => h.ActorId);
        b.Entity<Notification>().HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId);
    }

    public static TestDbContext Create() =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public int? UserId { get; set; }
    public string? FullName { get; set; } = "Test";
    public UserRole? Role { get; set; }

    public void As(User u) { UserId = u.Id; FullName = u.FullName; Role = u.Role; }
}

internal sealed class FakeEventPublisher : IEventPublisher
{
    public List<(IntegrationEvent Event, string RoutingKey)> Published { get; } = [];

    public Task PublishAsync<T>(T @event, string routingKey, CancellationToken ct = default) where T : IntegrationEvent
    {
        Published.Add((@event, routingKey));
        return Task.CompletedTask;
    }
}

internal sealed class NoCache : ICacheService
{
    public int Invalidations { get; private set; }
    public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default) => factory(ct);
    public Task RemoveAsync(string key, CancellationToken ct = default) { Invalidations++; return Task.CompletedTask; }
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) { Invalidations++; return Task.CompletedTask; }
}
