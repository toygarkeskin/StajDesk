using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Assets;

public sealed record AssetQuery : PagedQuery
{
    public AssetType? Type { get; init; }
    public AssetStatus? Status { get; init; }
    public int? StoreId { get; init; }
    public int? AssignedUserId { get; init; }
}

public sealed record AssetListItemDto(int Id, string Name, AssetType Type, AssetStatus Status, string InventoryNumber,
    string? SerialNumber, string? Manufacturer, string? Model, string? Location, StoreRefDto? Store, UserSummaryDto? AssignedUser,
    DateOnly? WarrantyEndDate, int OpenTicketCount);

public sealed record AssetDetailDto(int Id, string Name, AssetType Type, AssetStatus Status, string InventoryNumber,
    string? SerialNumber, string? Manufacturer, string? Model, string? Location, DateOnly? PurchaseDate,
    DateOnly? WarrantyEndDate, string? Notes, StoreRefDto? Store, UserSummaryDto? AssignedUser, DateTime CreatedAt, DateTime? UpdatedAt,
    IReadOnlyList<AssetTicketDto> Tickets, bool CanManage);

public sealed record AssetTicketDto(int Id, string Title, TicketStatus Status, TicketPriority Priority, DateTime CreatedAt);

public sealed record UpsertAssetRequest(string Name, AssetType Type, AssetStatus Status, string InventoryNumber,
    string? SerialNumber, string? Manufacturer, string? Model, string? Location, DateOnly? PurchaseDate,
    DateOnly? WarrantyEndDate, string? Notes, int? StoreId, int? AssignedUserId);

public interface IAssetService
{
    Task<PagedResult<AssetListItemDto>> GetListAsync(AssetQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<AssetListItemDto>> GetMineAsync(CancellationToken ct = default);
    Task<AssetDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AssetDetailDto> CreateAsync(UpsertAssetRequest request, CancellationToken ct = default);
    Task<AssetDetailDto> UpdateAsync(int id, UpsertAssetRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

internal sealed class AssetService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, ILogger<AssetService> logger) : IAssetService
{
    /// <summary>Envanter görünürlüğü: tümü / mağaza / kendisine zimmetli.</summary>
    private IQueryable<Asset> Visible()
    {
        currentUser.Require(Permissions.AssetsReadAll, Permissions.AssetsReadStore, Permissions.AssetsReadOwn);
        if (currentUser.Has(Permissions.AssetsReadAll)) return db.Assets;
        var userId = currentUser.Has(Permissions.AssetsReadOwn) ? currentUser.UserId : null;
        var storeId = currentUser.Has(Permissions.AssetsReadStore) ? currentUser.StoreId : null;
        return db.Assets.Where(a => (userId != null && a.AssignedUserId == userId) || (storeId != null && a.StoreId == storeId));
    }

    public async Task<PagedResult<AssetListItemDto>> GetListAsync(AssetQuery query, CancellationToken ct = default)
    {
        var q = Visible().AsNoTracking();
        if (query.Type is not null) q = q.Where(a => a.Type == query.Type);
        if (query.Status is not null) q = q.Where(a => a.Status == query.Status);
        if (query.StoreId is not null) q = q.Where(a => a.StoreId == query.StoreId);
        if (query.AssignedUserId is not null) q = q.Where(a => a.AssignedUserId == query.AssignedUserId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(a => a.Name.ToLower().Contains(s) || a.InventoryNumber.ToLower().Contains(s) ||
                             (a.SerialNumber != null && a.SerialNumber.ToLower().Contains(s)) || (a.Location != null && a.Location.ToLower().Contains(s)));
        }
        return await Project(q.OrderBy(a => a.InventoryNumber)).ToPagedResultAsync(query, ct);
    }

    public async Task<IReadOnlyList<AssetListItemDto>> GetMineAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var storeId = currentUser.Has(Permissions.AssetsReadStore) ? currentUser.StoreId : null;
        return await Project(db.Assets.AsNoTracking().Where(a => a.AssignedUserId == userId || (storeId != null && a.StoreId == storeId)).OrderBy(a => a.Name)).ToListAsync(ct);
    }

    public async Task<AssetDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var a = await Visible().AsNoTracking().Include(x => x.AssignedUser).Include(x => x.Store)
                    .Include(x => x.Tickets.OrderByDescending(t => t.CreatedAt).Take(20))
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Cihaz", id);
        return new AssetDetailDto(a.Id, a.Name, a.Type, a.Status, a.InventoryNumber, a.SerialNumber, a.Manufacturer, a.Model,
            a.Location, a.PurchaseDate, a.WarrantyEndDate, a.Notes,
            a.Store is null ? null : new StoreRefDto(a.Store.Id, a.Store.Code, a.Store.Name),
            a.AssignedUser is null ? null : new UserSummaryDto(a.AssignedUser.Id, a.AssignedUser.FullName, a.AssignedUser.Email),
            a.CreatedAt, a.UpdatedAt,
            a.Tickets.Select(t => new AssetTicketDto(t.Id, t.Title, t.Status, t.Priority, t.CreatedAt)).ToList(),
            currentUser.Has(Permissions.AssetsManage));
    }

    public async Task<AssetDetailDto> CreateAsync(UpsertAssetRequest request, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AssetsManage);
        await ValidateAsync(null, request, ct);
        var asset = new Asset();
        Apply(asset, request);
        db.Assets.Add(asset);
        await db.SaveChangesAsync(ct);
        audit.Add("asset.created", "asset", asset.Id);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Envanter kaydı oluşturuldu: {AssetId} {InventoryNumber}", asset.Id, asset.InventoryNumber);
        return await GetByIdAsync(asset.Id, ct);
    }

    public async Task<AssetDetailDto> UpdateAsync(int id, UpsertAssetRequest request, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AssetsManage);
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw NotFoundException.For("Cihaz", id);
        await ValidateAsync(id, request, ct);
        Apply(asset, request);
        asset.UpdatedAt = DateTime.UtcNow;
        audit.Add("asset.updated", "asset", asset.Id);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AssetsManage);
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw NotFoundException.For("Cihaz", id);
        if (await db.Tickets.AnyAsync(t => t.AssetId == id, ct))
            throw new ConflictException("Bu cihaza bağlı talepler var; silinemez. Durumunu 'Hurdaya ayrıldı' yapabilirsiniz.");
        db.Assets.Remove(asset);
        audit.Add("asset.deleted", "asset", id, new { asset.InventoryNumber });
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Envanter kaydı silindi: {AssetId}", id);
    }

    private async Task ValidateAsync(int? id, UpsertAssetRequest r, CancellationToken ct)
    {
        var inv = r.InventoryNumber.Trim();
        if (await db.Assets.AnyAsync(a => a.InventoryNumber == inv && a.Id != id, ct))
            throw new ConflictException("Bu envanter numarası başka bir kayıtta kullanılıyor.");
        if (r.AssignedUserId is not null && !await db.Users.AnyAsync(u => u.Id == r.AssignedUserId, ct)) throw NotFoundException.For("Kullanıcı", r.AssignedUserId);
        if (r.StoreId is not null && !await db.Stores.AnyAsync(s => s.Id == r.StoreId, ct)) throw NotFoundException.For("Mağaza", r.StoreId);
    }

    private static void Apply(Asset a, UpsertAssetRequest r)
    {
        a.Name = Text.Clean(r.Name);
        a.Type = r.Type;
        a.Status = r.Status;
        a.InventoryNumber = Text.Clean(r.InventoryNumber);
        a.SerialNumber = Text.CleanOrNull(r.SerialNumber);
        a.Manufacturer = Text.CleanOrNull(r.Manufacturer);
        a.Model = Text.CleanOrNull(r.Model);
        a.Location = Text.CleanOrNull(r.Location);
        a.PurchaseDate = r.PurchaseDate;
        a.WarrantyEndDate = r.WarrantyEndDate;
        a.Notes = Text.CleanOrNull(r.Notes);
        a.StoreId = r.StoreId;
        a.AssignedUserId = r.AssignedUserId;
    }

    private static IQueryable<AssetListItemDto> Project(IQueryable<Asset> q) =>
        q.Select(a => new AssetListItemDto(a.Id, a.Name, a.Type, a.Status, a.InventoryNumber, a.SerialNumber, a.Manufacturer,
            a.Model, a.Location,
            a.Store == null ? null : new StoreRefDto(a.Store.Id, a.Store.Code, a.Store.Name),
            a.AssignedUser == null ? null : new UserSummaryDto(a.AssignedUser.Id, a.AssignedUser.FullName, a.AssignedUser.Email),
            a.WarrantyEndDate, a.Tickets.Count(t => t.Status != TicketStatus.Closed)));
}

public sealed class UpsertAssetRequestValidator : AbstractValidator<UpsertAssetRequest>
{
    public UpsertAssetRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Cihaza tanınır bir ad verin.").MaximumLength(120).NoMarkup();
        RuleFor(x => x.InventoryNumber).NotEmpty().WithMessage("Envanter numarasını yazın (ör. INV-0042).").MaximumLength(50).NoMarkup();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.SerialNumber).MaximumLength(100).NoMarkup();
        RuleFor(x => x.Manufacturer).MaximumLength(100).NoMarkup();
        RuleFor(x => x.Model).MaximumLength(100).NoMarkup();
        RuleFor(x => x.Location).MaximumLength(120).NoMarkup();
        RuleFor(x => x.Notes).MaximumLength(2000).NoMarkup();
        RuleFor(x => x.WarrantyEndDate).GreaterThanOrEqualTo(x => x.PurchaseDate)
            .When(x => x.PurchaseDate is not null && x.WarrantyEndDate is not null)
            .WithMessage("Garanti bitişi, satın alma tarihinden sonra olmalı.");
    }
}
