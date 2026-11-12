using StajDesk.Application.Features.Employees;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Tickets;

/* =========================================================
   Okuma tarafı (Queries): AsNoTracking + doğrudan DTO projeksiyonu.
   Görünürlük kuralı (kendi / mağaza / tümü) her sorguya uygulanır.
   ========================================================= */

public sealed record ListTicketsQuery(TicketQuery Filter) : IQuery<PagedResult<TicketListItemDto>>;
public sealed record GetTicketQuery(int Id) : IQuery<TicketDetailDto>;

public static class TicketProjections
{
    public static IQueryable<TicketListItemDto> ToListItems(this IQueryable<Ticket> q, bool includeInternal) =>
        q.Select(t => new TicketListItemDto(
            t.Id, t.Title, t.Status, t.Priority, t.Type,
            new CategoryRefDto(t.Category.Id, t.Category.Name, t.Category.Color),
            t.Store == null ? null : new StoreRefDto(t.Store.Id, t.Store.Code, t.Store.Name),
            new UserSummaryDto(t.Requester.Id, t.Requester.FullName, t.Requester.Email),
            t.Assignee == null ? null : new UserSummaryDto(t.Assignee.Id, t.Assignee.FullName, t.Assignee.Email),
            t.Comments.Count(c => !c.IsInternal || includeInternal),
            t.CreatedAt, t.UpdatedAt, t.Department == null ? null : new RefDto(t.Department.Id, t.Department.Name)));
}

internal sealed class ListTicketsHandler(IAppDbContext db, ICurrentUser user, ITicketSearch? search = null) : IRequestHandler<ListTicketsQuery, PagedResult<TicketListItemDto>>
{
    public async Task<PagedResult<TicketListItemDto>> HandleAsync(ListTicketsQuery request, CancellationToken ct)
    {
        var f = request.Filter;
        var q = db.Tickets.AsNoTracking().Visible(user);

        if (f.Status is not null) q = q.Where(t => t.Status == f.Status);
        if (f.Priority is not null) q = q.Where(t => t.Priority == f.Priority);
        if (f.Type is not null) q = q.Where(t => t.Type == f.Type);
        if (f.CategoryId is not null) q = q.Where(t => t.CategoryId == f.CategoryId);
        if (f.StoreId is not null) q = q.Where(t => t.StoreId == f.StoreId);
        if (f.AssigneeId is not null) q = q.Where(t => t.AssigneeId == f.AssigneeId);
        if (f.AssetId is not null) q = q.Where(t => t.AssetId == f.AssetId);
        if (f.DepartmentId is not null) q = q.Where(t => t.DepartmentId == f.DepartmentId);
        if (f.Unassigned == true) q = q.Where(t => t.AssigneeId == null);
        if (f.ActiveOnly == true) q = q.Where(t => (t.Status != TicketStatus.Closed && t.Status != TicketStatus.Rejected));
        if (!string.IsNullOrWhiteSpace(f.Search)) q = (search ?? new ContainsTicketSearch()).Apply(q, f.Search);

        q = f.Sort?.ToLowerInvariant() switch
        {
            "oldest" => q.OrderBy(t => t.CreatedAt),
            "priority" => q.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt),
            "updated" => q.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt),
            _ => q.OrderByDescending(t => t.CreatedAt),
        };
        return await q.ToListItems(user.Has(Permissions.CommentsInternal)).ToPagedResultAsync(f, ct);
    }
}

internal sealed class GetTicketHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<GetTicketQuery, TicketDetailDto>
{
    public async Task<TicketDetailDto> HandleAsync(GetTicketQuery request, CancellationToken ct)
    {
        var t = await db.Tickets.AsNoTracking()
            .Include(x => x.Category).Include(x => x.Store).Include(x => x.Requester).Include(x => x.Assignee).Include(x => x.Asset).Include(x => x.Department)
            .Include(x => x.Comments.OrderBy(c => c.CreatedAt)).ThenInclude(c => c.Author)
            .Include(x => x.History.OrderBy(h => h.CreatedAt)).ThenInclude(h => h.Actor)
            .Include(x => x.Attachments.OrderBy(a => a.CreatedAt)).ThenInclude(a => a.UploadedBy)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct) ?? throw NotFoundException.For("Talep", request.Id);
        TicketAccess.EnsureCanSee(user, t);

        var showInternal = user.Has(Permissions.CommentsInternal);
        var authorIds = t.Comments.Select(c => c.AuthorId).Distinct().ToList();
        var staffIds = await db.UserRoles.Where(ur => authorIds.Contains(ur.UserId) && ur.Role.Permissions.Any(p => p.Permission.Key == Permissions.TicketsManage))
            .Select(ur => ur.UserId).Distinct().ToListAsync(ct);

        static UserSummaryDto Sum(User u) => new(u.Id, u.FullName, u.Email);
        return new TicketDetailDto(
            t.Id, t.Title, t.Description, t.Status, t.Priority, t.Type,
            new CategoryRefDto(t.Category.Id, t.Category.Name, t.Category.Color),
            t.Store is null ? null : new StoreRefDto(t.Store.Id, t.Store.Code, t.Store.Name),
            Sum(t.Requester), t.Assignee is null ? null : Sum(t.Assignee),
            t.Asset is null ? null : new AssetRefDto(t.Asset.Id, t.Asset.Name, t.Asset.InventoryNumber, t.Asset.Type),
            t.CreatedAt, t.UpdatedAt, t.ResolvedAt, t.ClosedAt,
            t.Comments.Where(c => showInternal || !c.IsInternal)
                .Select(c => new TicketCommentDto(c.Id, c.Body, c.IsInternal, new CommentAuthorDto(c.Author.Id, c.Author.FullName, staffIds.Contains(c.AuthorId)), c.CreatedAt)).ToList(),
            t.History.Select(h => new TicketHistoryDto(h.Id, h.Action, h.OldValue, h.NewValue, h.Actor is null ? null : Sum(h.Actor), h.CreatedAt)).ToList(),
            t.Attachments.Select(a => new TicketAttachmentDto(a.Id, a.FileName, a.ContentType, a.Size, Sum(a.UploadedBy), a.CreatedAt,
                AttachmentAccess.CanDelete(user, t, a))).ToList(),
            TicketAccess.AllowedStatuses(user, t),
            new TicketPermissionsDto(
                CanEdit: TicketAccess.CanEdit(user, t),
                CanManage: user.Has(Permissions.TicketsManage),
                CanDelete: user.Has(Permissions.TicketsDelete),
                CanComment: user.Has(Permissions.CommentsCreate) && (t.Status != TicketStatus.Closed && t.Status != TicketStatus.Rejected),
                CanInternal: showInternal,
                CanAttach: AttachmentAccess.CanUpload(user, t)),
            t.Department is null ? null : new RefDto(t.Department.Id, t.Department.Name));
    }
}
