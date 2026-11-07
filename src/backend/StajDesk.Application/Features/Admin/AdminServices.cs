using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;
using PermissionKeys = StajDesk.Domain.Security.Permissions;

namespace StajDesk.Application.Features.Admin;

/* =========================================================
   Mağazalar, roller & izinler, denetim kayıtları, e-posta kutusu
   ========================================================= */

public sealed record StoreDto(int Id, string Code, string Name, string? City, string? Address, string? Phone, bool IsActive,
    int UserCount, int OpenTicketCount, int AssetCount, IReadOnlyList<string> Owners);
public sealed record UpsertStoreRequest(string Code, string Name, string? City, string? Address, string? Phone, bool IsActive = true);

public sealed record RoleDto(int Id, string Name, string DisplayName, string? Description, bool IsSystem, int UserCount, IReadOnlyList<string> Permissions);
public sealed record PermissionDto(string Key, string Group, string Description);
public sealed record RolesOverviewDto(IReadOnlyList<RoleDto> Roles, IReadOnlyList<PermissionDto> Permissions);
public sealed record SetRolePermissionsRequest(IReadOnlyList<string> Permissions);

public sealed record AuditLogDto(long Id, string Action, string Entity, string? EntityId, string? Data, string? Ip, string? ActorName, DateTime CreatedAt);
public sealed record SentEmailDto(int Id, string ToAddress, string Subject, string Template, string Status, string? Error, DateTime CreatedAt);

public interface IAdminService
{
    Task<IReadOnlyList<StoreDto>> GetStoresAsync(CancellationToken ct = default);
    Task<StoreDto> SaveStoreAsync(int? id, UpsertStoreRequest request, CancellationToken ct = default);
    Task<RolesOverviewDto> GetRolesAsync(CancellationToken ct = default);
    Task SetRolePermissionsAsync(int roleId, IReadOnlyList<string> permissions, CancellationToken ct = default);
    Task<PagedResult<AuditLogDto>> GetAuditAsync(PagedQuery query, CancellationToken ct = default);
    Task<PagedResult<SentEmailDto>> GetSentEmailsAsync(PagedQuery query, CancellationToken ct = default);
}

public sealed record SimplePagedQuery : PagedQuery;

internal sealed class AdminService(IAppDbContext db, ICurrentUser currentUser, ICacheService cache, IAuditLogger audit) : IAdminService
{
    public async Task<IReadOnlyList<StoreDto>> GetStoresAsync(CancellationToken ct = default)
    {
        currentUser.Require(PermissionKeys.StoresRead, PermissionKeys.StoresManage);
        var q = db.Stores.AsNoTracking();
        // Yönetim yetkisi olmayan (ör. mağaza sahibi) yalnızca kendi mağazasını görür.
        if (!currentUser.HasAny(PermissionKeys.StoresManage, PermissionKeys.TicketsReadAll))
            q = q.Where(s => s.Id == currentUser.StoreId);

        return await q.OrderBy(s => s.Code)
            .Select(s => new StoreDto(s.Id, s.Code, s.Name, s.City, s.Address, s.Phone, s.IsActive,
                s.Users.Count,
                db.Tickets.Count(t => t.StoreId == s.Id && t.Status != TicketStatus.Closed),
                db.Assets.Count(a => a.StoreId == s.Id),
                s.Users.Where(u => u.Roles.Any(r => r.Role.Name == RoleNames.StoreOwner)).Select(u => u.FullName).ToList()))
            .ToListAsync(ct);
    }

    public async Task<StoreDto> SaveStoreAsync(int? id, UpsertStoreRequest r, CancellationToken ct = default)
    {
        currentUser.Require(PermissionKeys.StoresManage);
        var code = Text.Clean(r.Code).ToUpperInvariant();
        if (await db.Stores.AnyAsync(s => s.Code == code && s.Id != id, ct)) throw new ConflictException("Bu mağaza kodu başka bir mağazada kullanılıyor.");

        var store = id is null ? new Store() : await db.Stores.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw NotFoundException.For("Mağaza", id);
        store.Code = code;
        store.Name = Text.Clean(r.Name);
        store.City = Text.CleanOrNull(r.City);
        store.Address = Text.CleanOrNull(r.Address);
        store.Phone = Text.CleanOrNull(r.Phone);
        store.IsActive = r.IsActive;
        if (id is null) db.Stores.Add(store); else store.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        audit.Add(id is null ? "store.created" : "store.updated", "store", store.Id);
        await db.SaveChangesAsync(ct);
        return (await GetStoresAsync(ct)).First(s => s.Id == store.Id);
    }

    public async Task<RolesOverviewDto> GetRolesAsync(CancellationToken ct = default)
    {
        currentUser.Require(PermissionKeys.UsersManage, PermissionKeys.RolesManage);
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Id)
            .Select(r => new RoleDto(r.Id, r.Name, r.DisplayName, r.Description, r.IsSystem, r.Users.Count,
                r.Permissions.Select(p => p.Permission.Key).OrderBy(k => k).ToList()))
            .ToListAsync(ct);
        return new RolesOverviewDto(roles, PermissionKeys.All.Select(p => new PermissionDto(p.Key, p.Group, p.Description)).ToList());
    }

    public async Task SetRolePermissionsAsync(int roleId, IReadOnlyList<string> permissions, CancellationToken ct = default)
    {
        currentUser.Require(PermissionKeys.RolesManage);
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId, ct) ?? throw NotFoundException.For("Rol", roleId);
        if (role.Name == RoleNames.SystemAdmin) throw new Domain.Common.DomainException("Sistem Yöneticisi rolü her zaman tüm izinlere sahiptir; değiştirilemez.");
        var unknown = permissions.Where(p => !PermissionKeys.IsKnown(p)).ToList();
        if (unknown.Count > 0) throw new Domain.Common.DomainException($"Bilinmeyen izin: {string.Join(", ", unknown)}");

        var ids = await db.Permissions.Where(p => permissions.Contains(p.Key)).Select(p => p.Id).ToListAsync(ct);
        role.Permissions.Clear();
        foreach (var pid in ids) role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });
        audit.Add("role.permissions_changed", "role", role.Id, new { role.Name, permissions });
        await db.SaveChangesAsync(ct);
        // Tüm kullanıcıların izin önbelleği temizlenir; değişiklik bir sonraki istekte geçerli olur.
        await cache.RemoveByPrefixAsync(CacheKeys.PermissionsPrefix, ct);
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditAsync(PagedQuery query, CancellationToken ct = default)
    {
        currentUser.Require(PermissionKeys.AuditRead);
        var q = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(a => a.Action.Contains(s) || a.Entity.Contains(s));
        }
        return await q.OrderByDescending(a => a.CreatedAt)
            .Select(a => new AuditLogDto(a.Id, a.Action, a.Entity, a.EntityId, a.Data, a.Ip, a.Actor == null ? null : a.Actor.FullName, a.CreatedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<PagedResult<SentEmailDto>> GetSentEmailsAsync(PagedQuery query, CancellationToken ct = default)
    {
        currentUser.Require(PermissionKeys.MailRead);
        var q = db.SentEmails.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(m => m.ToAddress.Contains(s) || m.Subject.ToLower().Contains(s));
        }
        return await q.OrderByDescending(m => m.CreatedAt)
            .Select(m => new SentEmailDto(m.Id, m.ToAddress, m.Subject, m.Template, m.Status, m.Error, m.CreatedAt))
            .ToPagedResultAsync(query, ct);
    }
}

public sealed class UpsertStoreRequestValidator : AbstractValidator<UpsertStoreRequest>
{
    public UpsertStoreRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Mağaza kodunu girin (ör. IST-01).").MaximumLength(20).NoMarkup();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Mağaza adını girin.").MaximumLength(120).NoMarkup();
        RuleFor(x => x.City).MaximumLength(80).NoMarkup();
        RuleFor(x => x.Address).MaximumLength(250).NoMarkup();
        RuleFor(x => x.Phone).MaximumLength(30).NoMarkup();
    }
}
