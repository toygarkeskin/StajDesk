using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Contracts;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.Application.Features.Tickets;

public interface ITicketService
{
    Task<PagedResult<TicketListItemDto>> GetListAsync(TicketQuery query, CancellationToken ct = default);
    Task<TicketDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken ct = default);
    Task<TicketDetailDto> UpdateAsync(int id, UpdateTicketRequest request, CancellationToken ct = default);
    Task<TicketDetailDto> ChangeStatusAsync(int id, TicketStatus status, CancellationToken ct = default);
    Task<TicketDetailDto> ChangePriorityAsync(int id, TicketPriority priority, CancellationToken ct = default);
    Task<TicketDetailDto> AssignAsync(int id, int? assigneeId, CancellationToken ct = default);
    Task<TicketCommentDto> AddCommentAsync(int id, AddCommentRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

internal sealed class TicketService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IEventPublisher events,
    ICacheService cache,
    ILogger<TicketService> logger) : ITicketService
{
    public async Task<PagedResult<TicketListItemDto>> GetListAsync(TicketQuery query, CancellationToken ct = default)
    {
        var q = VisibleTickets().AsNoTracking();

        if (query.Status is not null) q = q.Where(t => t.Status == query.Status);
        if (query.Priority is not null) q = q.Where(t => t.Priority == query.Priority);
        if (query.Type is not null) q = q.Where(t => t.Type == query.Type);
        if (query.CategoryId is not null) q = q.Where(t => t.CategoryId == query.CategoryId);
        if (query.AssigneeId is not null) q = q.Where(t => t.AssigneeId == query.AssigneeId);
        if (query.RequesterId is not null) q = q.Where(t => t.RequesterId == query.RequesterId);
        if (query.AssetId is not null) q = q.Where(t => t.AssetId == query.AssetId);
        if (query.Unassigned == true) q = q.Where(t => t.AssigneeId == null);
        if (query.ActiveOnly == true) q = q.Where(t => t.Status != TicketStatus.Closed);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            if (int.TryParse(s.TrimStart('#'), out var idSearch))
                q = q.Where(t => t.Id == idSearch || t.Title.ToLower().Contains(s));
            else
                q = q.Where(t => t.Title.ToLower().Contains(s) || t.Description.ToLower().Contains(s));
        }

        q = query.Sort?.ToLowerInvariant() switch
        {
            "oldest" => q.OrderBy(t => t.CreatedAt),
            "priority" => q.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt),
            "updated" => q.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt),
            _ => q.OrderByDescending(t => t.CreatedAt)
        };

        var isStaff = currentUser.IsStaff;
        return await q.Select(t => new TicketListItemDto(
                t.Id, t.Title, t.Status, t.Priority, t.Type,
                new CategoryRefDto(t.Category.Id, t.Category.Name, t.Category.Color),
                new UserSummaryDto(t.Requester.Id, t.Requester.FullName, t.Requester.Email, t.Requester.Role),
                t.Assignee == null ? null : new UserSummaryDto(t.Assignee.Id, t.Assignee.FullName, t.Assignee.Email, t.Assignee.Role),
                t.Comments.Count(c => !c.IsInternal || isStaff),
                t.CreatedAt, t.UpdatedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<TicketDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Requester)
            .Include(t => t.Assignee)
            .Include(t => t.Asset)
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt)).ThenInclude(c => c.Author)
            .Include(t => t.History.OrderBy(h => h.CreatedAt)).ThenInclude(h => h.Actor)
            .FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw NotFoundException.For("Talep", id);

        EnsureCanView(ticket);
        return ToDetail(ticket);
    }

    public async Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, ct)
                       ?? throw new NotFoundException("Seçilen kategori bulunamadı veya aktif değil.");
        await EnsureAssetUsableAsync(request.AssetId, userId, ct);

        var ticket = new Ticket(request.Title, request.Description, userId, category.Id, request.Priority, request.Type, request.AssetId);
        ticket.History.Add(new TicketHistory { ActorId = userId, Action = TicketHistoryAction.Created, NewValue = ticket.Status.ToString() });
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Talep oluşturuldu: #{TicketId} kategori={Category} öncelik={Priority} kullanıcı={UserId}",
            ticket.Id, category.Name, ticket.Priority, userId);

        await events.PublishAsync(new TicketCreatedEvent(ticket.Id, ticket.Title, ticket.Priority.ToString(), category.Name,
            userId, currentUser.FullName ?? ""), Messaging.RoutingKeys.TicketCreated, ct);
        await InvalidateStatsAsync(ct);

        return await GetByIdAsync(ticket.Id, ct);
    }

    public async Task<TicketDetailDto> UpdateAsync(int id, UpdateTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadAsync(id, ct);
        var userId = currentUser.RequireUserId();

        // Kullanıcı yalnızca kendi talebini ve henüz kimse üzerinde çalışmıyorken düzenleyebilir.
        if (!currentUser.IsStaff && (ticket.RequesterId != userId || ticket.Status != TicketStatus.Open))
            throw new ForbiddenException("Bu talebi yalnızca açık durumdayken ve talep sahibi olarak düzenleyebilirsiniz.");

        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            throw new NotFoundException("Seçilen kategori bulunamadı.");
        await EnsureAssetUsableAsync(request.AssetId, ticket.RequesterId, ct);

        ticket.UpdateDetails(request.Title, request.Description, request.CategoryId, request.Type, request.AssetId);
        AddHistory(ticket, TicketHistoryAction.Updated, null, null);
        await db.SaveChangesAsync(ct);
        await InvalidateStatsAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<TicketDetailDto> ChangeStatusAsync(int id, TicketStatus status, CancellationToken ct = default)
    {
        var ticket = await LoadAsync(id, ct);
        var userId = currentUser.RequireUserId();
        EnsureCanView(ticket);

        if (!GetAllowedStatuses(ticket).Contains(status))
        {
            if (!currentUser.IsStaff && ticket.Status != TicketStatus.Resolved)
                throw new ForbiddenException("Talep durumunu yalnızca destek personeli değiştirebilir.");
            throw new Domain.Common.DomainException($"Talep '{ticket.Status}' durumundan '{status}' durumuna geçirilemez.");
        }

        var old = ticket.Status;
        ticket.ChangeStatus(status);
        AddHistory(ticket, TicketHistoryAction.StatusChanged, old.ToString(), status.ToString());
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Talep durumu değişti: #{TicketId} {Old} -> {New} (kullanıcı {UserId})", id, old, status, userId);
        await events.PublishAsync(new TicketStatusChangedEvent(ticket.Id, ticket.Title, old.ToString(), status.ToString(),
            ticket.RequesterId, ticket.AssigneeId, userId, currentUser.FullName ?? ""), Messaging.RoutingKeys.TicketStatusChanged, ct);
        await InvalidateStatsAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<TicketDetailDto> ChangePriorityAsync(int id, TicketPriority priority, CancellationToken ct = default)
    {
        EnsureStaff();
        var ticket = await LoadAsync(id, ct);
        var old = ticket.Priority;
        if (old == priority) return await GetByIdAsync(id, ct);

        ticket.ChangePriority(priority);
        AddHistory(ticket, TicketHistoryAction.PriorityChanged, old.ToString(), priority.ToString());
        await db.SaveChangesAsync(ct);
        await InvalidateStatsAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<TicketDetailDto> AssignAsync(int id, int? assigneeId, CancellationToken ct = default)
    {
        EnsureStaff();
        var userId = currentUser.RequireUserId();
        var ticket = await LoadAsync(id, ct);

        User? assignee = null;
        if (assigneeId is not null)
        {
            assignee = await db.Users.FirstOrDefaultAsync(u => u.Id == assigneeId, ct)
                       ?? throw NotFoundException.For("Kullanıcı", assigneeId);
            if (!assignee.IsStaff || !assignee.IsActive)
                throw new Domain.Common.DomainException("Talepler yalnızca aktif destek personeline atanabilir.");
        }
        if (ticket.AssigneeId == assigneeId) return await GetByIdAsync(id, ct);

        var oldName = ticket.AssigneeId is null ? null :
            await db.Users.Where(u => u.Id == ticket.AssigneeId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        ticket.AssignTo(assigneeId);
        AddHistory(ticket, TicketHistoryAction.Assigned, oldName, assignee?.FullName);

        // GLPI davranışı: açık bir talep birine atandığında otomatik olarak "İşlemde" olur.
        string? oldStatus = null;
        if (assignee is not null && ticket.Status == TicketStatus.Open)
        {
            oldStatus = ticket.Status.ToString();
            ticket.ChangeStatus(TicketStatus.InProgress);
            AddHistory(ticket, TicketHistoryAction.StatusChanged, oldStatus, ticket.Status.ToString());
        }
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Talep atandı: #{TicketId} -> {AssigneeId}", id, assigneeId);
        await events.PublishAsync(new TicketAssignedEvent(ticket.Id, ticket.Title, assigneeId, assignee?.FullName,
            ticket.RequesterId, userId), Messaging.RoutingKeys.TicketAssigned, ct);
        if (oldStatus is not null)
            await events.PublishAsync(new TicketStatusChangedEvent(ticket.Id, ticket.Title, oldStatus, ticket.Status.ToString(),
                ticket.RequesterId, ticket.AssigneeId, userId, currentUser.FullName ?? ""), Messaging.RoutingKeys.TicketStatusChanged, ct);
        await InvalidateStatsAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<TicketCommentDto> AddCommentAsync(int id, AddCommentRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var ticket = await LoadAsync(id, ct);
        EnsureCanView(ticket);

        if (ticket.Status == TicketStatus.Closed)
            throw new Domain.Common.DomainException("Kapanmış bir talebe yorum eklenemez.");
        if (request.IsInternal && !currentUser.IsStaff)
            throw new ForbiddenException("Dahili not yalnızca destek personeli tarafından eklenebilir.");

        var comment = new TicketComment { TicketId = ticket.Id, AuthorId = userId, Body = request.Body.Trim(), IsInternal = request.IsInternal };
        db.TicketComments.Add(comment);
        await db.SaveChangesAsync(ct);

        await events.PublishAsync(new TicketCommentAddedEvent(ticket.Id, ticket.Title, comment.Id, userId, currentUser.FullName ?? "",
            currentUser.IsStaff, comment.IsInternal, ticket.RequesterId, ticket.AssigneeId), Messaging.RoutingKeys.TicketCommentAdded, ct);

        var author = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new UserSummaryDto(u.Id, u.FullName, u.Email, u.Role)).FirstAsync(ct);
        return new TicketCommentDto(comment.Id, comment.Body, comment.IsInternal, author, comment.CreatedAt);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin) throw new ForbiddenException("Talepleri yalnızca yönetici silebilir.");
        var ticket = await LoadAsync(id, ct);
        db.Tickets.Remove(ticket);
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Talep silindi: #{TicketId} (yönetici {UserId})", id, currentUser.UserId);
        await InvalidateStatsAsync(ct);
    }

    // ---------- yardımcılar ----------

    /// <summary>Kullanıcı yalnızca kendi taleplerini, destek personeli tüm talepleri görür.</summary>
    private IQueryable<Ticket> VisibleTickets()
    {
        var userId = currentUser.RequireUserId();
        return currentUser.IsStaff ? db.Tickets : db.Tickets.Where(t => t.RequesterId == userId);
    }

    private async Task<Ticket> LoadAsync(int id, CancellationToken ct) =>
        await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw NotFoundException.For("Talep", id);

    private void EnsureCanView(Ticket ticket)
    {
        if (!currentUser.IsStaff && ticket.RequesterId != currentUser.UserId)
            throw new ForbiddenException("Bu talebi görüntüleme yetkiniz bulunmamaktadır.");
    }

    private void EnsureStaff()
    {
        if (!currentUser.IsStaff) throw new ForbiddenException("Bu işlemi yalnızca destek personeli yapabilir.");
    }

    private async Task EnsureAssetUsableAsync(int? assetId, int requesterId, CancellationToken ct)
    {
        if (assetId is null) return;
        var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId, ct)
                    ?? throw NotFoundException.For("Envanter kaydı", assetId);
        if (!currentUser.IsStaff && asset.AssignedUserId != requesterId)
            throw new ForbiddenException("Yalnızca size zimmetli cihazları seçebilirsiniz.");
    }

    /// <summary>Mevcut kullanıcının bu talebi hangi durumlara geçirebileceği.</summary>
    internal IReadOnlyList<TicketStatus> GetAllowedStatuses(Ticket ticket)
    {
        var all = Enum.GetValues<TicketStatus>().Where(s => s != ticket.Status && Ticket.CanTransition(ticket.Status, s));
        if (currentUser.IsStaff) return all.ToList();

        // Talep sahibi: çözülen talebi onaylayıp kapatabilir veya "çözülmedi" diyerek yeniden açabilir.
        if (ticket.RequesterId == currentUser.UserId && ticket.Status == TicketStatus.Resolved)
            return all.Where(s => s is TicketStatus.Closed or TicketStatus.InProgress).ToList();
        return [];
    }

    private void AddHistory(Ticket ticket, TicketHistoryAction action, string? oldValue, string? newValue) =>
        db.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id, ActorId = currentUser.UserId, Action = action, OldValue = oldValue, NewValue = newValue
        });

    private Task InvalidateStatsAsync(CancellationToken ct) => cache.RemoveByPrefixAsync(CacheKeys.StatsPrefix, ct);

    private TicketDetailDto ToDetail(Ticket t)
    {
        static UserSummaryDto Sum(User u) => new(u.Id, u.FullName, u.Email, u.Role);
        return new TicketDetailDto(
            t.Id, t.Title, t.Description, t.Status, t.Priority, t.Type,
            new CategoryRefDto(t.Category.Id, t.Category.Name, t.Category.Color),
            Sum(t.Requester),
            t.Assignee is null ? null : Sum(t.Assignee),
            t.Asset is null ? null : new AssetRefDto(t.Asset.Id, t.Asset.Name, t.Asset.InventoryNumber, t.Asset.Type),
            t.CreatedAt, t.UpdatedAt, t.ResolvedAt, t.ClosedAt,
            t.Comments.Where(c => currentUser.IsStaff || !c.IsInternal)
                .Select(c => new TicketCommentDto(c.Id, c.Body, c.IsInternal, Sum(c.Author), c.CreatedAt)).ToList(),
            t.History.Select(h => new TicketHistoryDto(h.Id, h.Action, h.OldValue, h.NewValue,
                h.Actor is null ? null : Sum(h.Actor), h.CreatedAt)).ToList(),
            GetAllowedStatuses(t));
    }
}
