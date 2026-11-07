using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Contracts;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Tickets;

/* =========================================================
   Yazma tarafı (Commands). Her komut:
   1) izin kontrolü  2) domain metodu (iş kuralı)  3) geçmiş kaydı
   4) olayı outbox'a ekleme  5) tek transaction'da kaydetme
   ========================================================= */

public sealed record CreateTicketCommand(CreateTicketRequest Request) : ICommand<int>;
public sealed record UpdateTicketCommand(int Id, UpdateTicketRequest Request) : ICommand<bool>;
public sealed record ChangeTicketStatusCommand(int Id, TicketStatus Status) : ICommand<bool>;
public sealed record ChangeTicketPriorityCommand(int Id, TicketPriority Priority) : ICommand<bool>;
public sealed record AssignTicketCommand(int Id, int? AssigneeId) : ICommand<bool>;
public sealed record AddCommentCommand(int Id, AddCommentRequest Request) : ICommand<int>;
public sealed record DeleteTicketCommand(int Id) : ICommand<bool>;

/// <summary>Komut handler'larının ortak yardımcıları.</summary>
internal abstract class TicketCommandBase(IAppDbContext db, ICurrentUser user, ICacheService cache)
{
    protected IAppDbContext Db => db;
    protected ICurrentUser User => user;

    protected async Task<Ticket> LoadAsync(int id, CancellationToken ct) =>
        await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw NotFoundException.For("Talep", id);

    protected void History(Ticket t, TicketHistoryAction action, string? oldValue, string? newValue) =>
        db.TicketHistories.Add(new TicketHistory { TicketId = t.Id, ActorId = user.UserId, Action = action, OldValue = oldValue, NewValue = newValue });

    protected Task InvalidateStatsAsync(CancellationToken ct) => cache.RemoveByPrefixAsync(CacheKeys.StatsPrefix, ct);
}

internal sealed class CreateTicketHandler(IAppDbContext db, ICurrentUser user, ICacheService cache, IEventPublisher events, IUnitOfWork uow, IAuditLogger audit, ILogger<CreateTicketHandler> logger)
    : TicketCommandBase(db, user, cache), IRequestHandler<CreateTicketCommand, int>
{
    public async Task<int> HandleAsync(CreateTicketCommand command, CancellationToken ct)
    {
        User.Require(Permissions.TicketsCreate);
        var userId = User.RequireUserId();
        var r = command.Request;
        var category = await Db.Categories.FirstOrDefaultAsync(c => c.Id == r.CategoryId && c.IsActive, ct)
                       ?? throw new NotFoundException("Seçilen kategori bulunamadı veya aktif değil.");
        if (r.AssetId is not null)
        {
            var asset = await Db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == r.AssetId, ct) ?? throw NotFoundException.For("Cihaz", r.AssetId);
            var allowed = User.Has(Permissions.AssetsReadAll) || asset.AssignedUserId == userId
                          || (User.Has(Permissions.AssetsReadStore) && asset.StoreId != null && asset.StoreId == User.StoreId);
            if (!allowed) throw new ForbiddenException("Yalnızca size veya mağazanıza ait cihazları seçebilirsiniz.");
        }

        var id = await uow.ExecuteAsync(async token =>
        {
            var ticket = new Ticket(Text.Clean(r.Title), Text.Clean(r.Description), userId, category.Id, r.Priority, r.Type, r.AssetId, User.StoreId);
            ticket.History.Add(new TicketHistory { ActorId = userId, Action = TicketHistoryAction.Created, NewValue = nameof(TicketStatus.Open) });
            Db.Tickets.Add(ticket);
            await Db.SaveChangesAsync(token); // kimlik (Id) üretildi

            events.Enqueue(new TicketCreatedEvent(ticket.Id, ticket.Title, ticket.Priority.ToString(), category.Name, userId, User.FullName ?? "", User.StoreId),
                Messaging.RoutingKeys.TicketCreated);
            audit.Add("ticket.created", "ticket", ticket.Id);
            await Db.SaveChangesAsync(token);
            return ticket.Id;
        }, ct);

        await InvalidateStatsAsync(ct);
        logger.LogInformation("Talep oluşturuldu: #{TicketId} kategori={Category} öncelik={Priority} kullanıcı={UserId}", id, category.Name, r.Priority, userId);
        return id;
    }
}

internal sealed class UpdateTicketHandler(IAppDbContext db, ICurrentUser user, ICacheService cache)
    : TicketCommandBase(db, user, cache), IRequestHandler<UpdateTicketCommand, bool>
{
    public async Task<bool> HandleAsync(UpdateTicketCommand command, CancellationToken ct)
    {
        var ticket = await LoadAsync(command.Id, ct);
        TicketAccess.EnsureCanSee(User, ticket);
        if (!TicketAccess.CanEdit(User, ticket))
            throw new ForbiddenException("Bu talebi yalnızca açık durumdayken ve talep sahibi olarak düzenleyebilirsiniz.");
        var r = command.Request;
        if (!await Db.Categories.AnyAsync(c => c.Id == r.CategoryId, ct)) throw new NotFoundException("Seçilen kategori bulunamadı.");

        ticket.UpdateDetails(Text.Clean(r.Title), Text.Clean(r.Description), r.CategoryId, r.Type, r.AssetId);
        History(ticket, TicketHistoryAction.Updated, null, null);
        await Db.SaveChangesAsync(ct);
        await InvalidateStatsAsync(ct);
        return true;
    }
}

internal sealed class ChangeTicketStatusHandler(IAppDbContext db, ICurrentUser user, ICacheService cache, IEventPublisher events, ILogger<ChangeTicketStatusHandler> logger)
    : TicketCommandBase(db, user, cache), IRequestHandler<ChangeTicketStatusCommand, bool>
{
    public async Task<bool> HandleAsync(ChangeTicketStatusCommand command, CancellationToken ct)
    {
        var userId = User.RequireUserId();
        var ticket = await LoadAsync(command.Id, ct);
        TicketAccess.EnsureCanSee(User, ticket);
        if (!TicketAccess.AllowedStatuses(User, ticket).Contains(command.Status))
        {
            if (!User.Has(Permissions.TicketsManage) && ticket.Status != TicketStatus.Resolved)
                throw new ForbiddenException("Talep durumunu yalnızca destek personeli değiştirebilir.");
            throw new DomainException($"Talep '{ticket.Status}' durumundan '{command.Status}' durumuna geçirilemez.");
        }

        var old = ticket.Status;
        ticket.ChangeStatus(command.Status);
        History(ticket, TicketHistoryAction.StatusChanged, old.ToString(), command.Status.ToString());
        events.Enqueue(new TicketStatusChangedEvent(ticket.Id, ticket.Title, old.ToString(), command.Status.ToString(),
            ticket.RequesterId, ticket.AssigneeId, userId, User.FullName ?? ""), Messaging.RoutingKeys.TicketStatusChanged);
        await Db.SaveChangesAsync(ct); // talep + geçmiş + outbox tek transaction

        logger.LogInformation("Talep durumu değişti: #{TicketId} {Old} -> {New} (kullanıcı {UserId})", ticket.Id, old, command.Status, userId);
        await InvalidateStatsAsync(ct);
        return true;
    }
}

internal sealed class ChangeTicketPriorityHandler(IAppDbContext db, ICurrentUser user, ICacheService cache)
    : TicketCommandBase(db, user, cache), IRequestHandler<ChangeTicketPriorityCommand, bool>
{
    public async Task<bool> HandleAsync(ChangeTicketPriorityCommand command, CancellationToken ct)
    {
        User.Require(Permissions.TicketsManage);
        var ticket = await LoadAsync(command.Id, ct);
        if (ticket.Priority == command.Priority) return true;
        var old = ticket.Priority;
        ticket.ChangePriority(command.Priority);
        History(ticket, TicketHistoryAction.PriorityChanged, old.ToString(), command.Priority.ToString());
        await Db.SaveChangesAsync(ct);
        await InvalidateStatsAsync(ct);
        return true;
    }
}

internal sealed class AssignTicketHandler(IAppDbContext db, ICurrentUser user, ICacheService cache, IEventPublisher events)
    : TicketCommandBase(db, user, cache), IRequestHandler<AssignTicketCommand, bool>
{
    public async Task<bool> HandleAsync(AssignTicketCommand command, CancellationToken ct)
    {
        User.Require(Permissions.TicketsManage);
        var userId = User.RequireUserId();
        var ticket = await LoadAsync(command.Id, ct);
        if (ticket.AssigneeId == command.AssigneeId) return true;

        User? assignee = null;
        if (command.AssigneeId is not null)
        {
            assignee = await Db.Users.FirstOrDefaultAsync(u => u.Id == command.AssigneeId, ct) ?? throw NotFoundException.For("Kullanıcı", command.AssigneeId);
            var isAgent = await Db.UserRoles.AnyAsync(ur => ur.UserId == assignee.Id && ur.Role.Permissions.Any(p => p.Permission.Key == Permissions.TicketsManage), ct);
            if (!assignee.IsActive || !isAgent) throw new DomainException("Talepler yalnızca aktif destek personeline atanabilir.");
        }

        var oldName = ticket.AssigneeId is null ? null : await Db.Users.Where(u => u.Id == ticket.AssigneeId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        ticket.AssignTo(command.AssigneeId);
        History(ticket, TicketHistoryAction.Assigned, oldName, assignee?.FullName);
        events.Enqueue(new TicketAssignedEvent(ticket.Id, ticket.Title, command.AssigneeId, assignee?.FullName, ticket.RequesterId, userId), Messaging.RoutingKeys.TicketAssigned);

        // GLPI davranışı: açık bir talep birine atanınca otomatik "İşlemde" olur.
        if (assignee is not null && ticket.Status == TicketStatus.Open)
        {
            ticket.ChangeStatus(TicketStatus.InProgress);
            History(ticket, TicketHistoryAction.StatusChanged, nameof(TicketStatus.Open), nameof(TicketStatus.InProgress));
            events.Enqueue(new TicketStatusChangedEvent(ticket.Id, ticket.Title, nameof(TicketStatus.Open), nameof(TicketStatus.InProgress),
                ticket.RequesterId, ticket.AssigneeId, userId, User.FullName ?? ""), Messaging.RoutingKeys.TicketStatusChanged);
        }
        await Db.SaveChangesAsync(ct);
        await InvalidateStatsAsync(ct);
        return true;
    }
}

internal sealed class AddCommentHandler(IAppDbContext db, ICurrentUser user, ICacheService cache, IEventPublisher events, IUnitOfWork uow)
    : TicketCommandBase(db, user, cache), IRequestHandler<AddCommentCommand, int>
{
    public async Task<int> HandleAsync(AddCommentCommand command, CancellationToken ct)
    {
        User.Require(Permissions.CommentsCreate);
        var userId = User.RequireUserId();
        var r = command.Request;
        if (r.IsInternal && !User.Has(Permissions.CommentsInternal))
            throw new ForbiddenException("Dahili not yalnızca destek personeli tarafından eklenebilir.");
        var ticket = await LoadAsync(command.Id, ct);
        TicketAccess.EnsureCanSee(User, ticket);
        if (ticket.Status == TicketStatus.Closed) throw new DomainException("Kapanmış bir talebe yorum eklenemez; önce talebi yeniden açın.");

        return await uow.ExecuteAsync(async token =>
        {
            var comment = new TicketComment { TicketId = ticket.Id, AuthorId = userId, Body = Text.Clean(r.Body), IsInternal = r.IsInternal };
            Db.TicketComments.Add(comment);
            await Db.SaveChangesAsync(token);
            events.Enqueue(new TicketCommentAddedEvent(ticket.Id, ticket.Title, comment.Id, userId, User.FullName ?? "", User.IsStaff,
                comment.IsInternal, ticket.RequesterId, ticket.AssigneeId), Messaging.RoutingKeys.TicketCommentAdded);
            await Db.SaveChangesAsync(token);
            return comment.Id;
        }, ct);
    }
}

internal sealed class DeleteTicketHandler(IAppDbContext db, ICurrentUser user, ICacheService cache, IAuditLogger audit, ILogger<DeleteTicketHandler> logger)
    : TicketCommandBase(db, user, cache), IRequestHandler<DeleteTicketCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteTicketCommand command, CancellationToken ct)
    {
        User.Require(Permissions.TicketsDelete);
        var ticket = await LoadAsync(command.Id, ct);
        Db.Tickets.Remove(ticket);
        audit.Add("ticket.deleted", "ticket", ticket.Id, new { ticket.Title });
        await Db.SaveChangesAsync(ct);
        logger.LogWarning("Talep silindi: #{TicketId} (kullanıcı {UserId})", command.Id, User.UserId);
        await InvalidateStatsAsync(ct);
        return true;
    }
}
