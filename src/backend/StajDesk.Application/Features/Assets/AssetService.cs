using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.Application.Features.Assets;

public sealed record AssetQuery : PagedQuery
{
    public AssetType? Type { get; init; }
    public AssetStatus? Status { get; init; }
    public int? AssignedUserId { get; init; }
}

public sealed record AssetListItemDto(int Id, string Name, AssetType Type, AssetStatus Status, string InventoryNumber,
    string? SerialNumber, string? Manufacturer, string? Model, string? Location, UserSummaryDto? AssignedUser,
    DateOnly? WarrantyEndDate, int OpenTicketCount);

public sealed record AssetDetailDto(int Id, string Name, AssetType Type, AssetStatus Status, string InventoryNumber,
    string? SerialNumber, string? Manufacturer, string? Model, string? Location, DateOnly? PurchaseDate,
    DateOnly? WarrantyEndDate, string? Notes, UserSummaryDto? AssignedUser, DateTime CreatedAt, DateTime? UpdatedAt,
    IReadOnlyList<AssetTicketDto> Tickets);

public sealed record AssetTicketDto(int Id, string Title, TicketStatus Status, TicketPriority Priority, DateTime CreatedAt);

public sealed record UpsertAssetRequest(string Name, AssetType Type, AssetStatus Status, string InventoryNumber,
    string? SerialNumber, string? Manufacturer, string? Model, string? Location, DateOnly? PurchaseDate,
    DateOnly? WarrantyEndDate, string? Notes, int? AssignedUserId);

public interface IAssetService
{
    Task<PagedResult<AssetListItemDto>> GetListAsync(AssetQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<AssetListItemDto>> GetMineAsync(CancellationToken ct = default);
    Task<AssetDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AssetDetailDto> CreateAsync(UpsertAssetRequest request, CancellationToken ct = default);
    Task<AssetDetailDto> UpdateAsync(int id, UpsertAssetRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

internal sealed class AssetService(IAppDbContext db, ICurrentUser currentUser, ILogger<AssetService> logger) : IAssetService
{
    public async Task<PagedResult<AssetListItemDto>> GetListAsync(AssetQuery query, CancellationToken ct = default)
    {
        var q = db.Assets.AsNoTracking();
        if (query.Type is not null) q = q.Where(a => a.Type == query.Type);
        if (query.Status is not null) q = q.Where(a => a.Status == query.Status);
        if (query.AssignedUserId is not null) q = q.Where(a => a.AssignedUserId == query.AssignedUserId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(a => a.Name.ToLower().Contains(s) || a.InventoryNumber.ToLower().Contains(s) ||
                             (a.SerialNumber != null && a.SerialNumber.ToLower().Contains(s)) ||
                             (a.Location != null && a.Location.ToLower().Contains(s)));
        }
        return await Project(q.OrderBy(a => a.InventoryNumber)).ToPagedResultAsync(query, ct);
    }

    public async Task<IReadOnlyList<AssetListItemDto>> GetMineAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        return await Project(db.Assets.AsNoTracking().Where(a => a.AssignedUserId == userId).OrderBy(a => a.Name)).ToListAsync(ct);
    }

    public async Task<AssetDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var a = await db.Assets.AsNoTracking()
            .Include(x => x.AssignedUser)
            .Include(x => x.Tickets.OrderByDescending(t => t.CreatedAt).Take(20))
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Envanter kaydı", id);

        if (!currentUser.IsStaff && a.AssignedUserId != currentUser.UserId)
            throw new ForbiddenException();

        return new AssetDetailDto(a.Id, a.Name, a.Type, a.Status, a.InventoryNumber, a.SerialNumber, a.Manufacturer, a.Model,
            a.Location, a.PurchaseDate, a.WarrantyEndDate, a.Notes,
            a.AssignedUser is null ? null : new UserSummaryDto(a.AssignedUser.Id, a.AssignedUser.FullName, a.AssignedUser.Email, a.AssignedUser.Role),
            a.CreatedAt, a.UpdatedAt,
            a.Tickets.Select(t => new AssetTicketDto(t.Id, t.Title, t.Status, t.Priority, t.CreatedAt)).ToList());
    }

    public async Task<AssetDetailDto> CreateAsync(UpsertAssetRequest request, CancellationToken ct = default)
    {
        await ValidateAsync(null, request, ct);
        var asset = new Asset();
        Apply(asset, request);
        db.Assets.Add(asset);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Envanter kaydı oluşturuldu: {AssetId} {InventoryNumber}", asset.Id, asset.InventoryNumber);
        return await GetByIdAsync(asset.Id, ct);
    }

    public async Task<AssetDetailDto> UpdateAsync(int id, UpsertAssetRequest request, CancellationToken ct = default)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw NotFoundException.For("Envanter kaydı", id);
        await ValidateAsync(id, request, ct);
        Apply(asset, request);
        asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin) throw new ForbiddenException("Envanter kayıtlarını yalnızca yönetici silebilir.");
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw NotFoundException.For("Envanter kaydı", id);
        if (await db.Tickets.AnyAsync(t => t.AssetId == id, ct))
            throw new ConflictException("Bu cihaza bağlı talepler bulunduğu için silinemez. Durumunu 'Hurdaya ayrıldı' yapabilirsiniz.");
        db.Assets.Remove(asset);
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Envanter kaydı silindi: {AssetId}", id);
    }

    private async Task ValidateAsync(int? id, UpsertAssetRequest r, CancellationToken ct)
    {
        var inv = r.InventoryNumber.Trim();
        if (await db.Assets.AnyAsync(a => a.InventoryNumber == inv && a.Id != id, ct))
            throw new ConflictException("Bu envanter numarası başka bir kayıtta kullanılıyor.");
        if (r.AssignedUserId is not null && !await db.Users.AnyAsync(u => u.Id == r.AssignedUserId, ct))
            throw NotFoundException.For("Kullanıcı", r.AssignedUserId);
    }

    private static void Apply(Asset a, UpsertAssetRequest r)
    {
        a.Name = r.Name.Trim();
        a.Type = r.Type;
        a.Status = r.Status;
        a.InventoryNumber = r.InventoryNumber.Trim();
        a.SerialNumber = r.SerialNumber?.Trim();
        a.Manufacturer = r.Manufacturer?.Trim();
        a.Model = r.Model?.Trim();
        a.Location = r.Location?.Trim();
        a.PurchaseDate = r.PurchaseDate;
        a.WarrantyEndDate = r.WarrantyEndDate;
        a.Notes = r.Notes?.Trim();
        a.AssignedUserId = r.AssignedUserId;
    }

    private static IQueryable<AssetListItemDto> Project(IQueryable<Asset> q) =>
        q.Select(a => new AssetListItemDto(a.Id, a.Name, a.Type, a.Status, a.InventoryNumber, a.SerialNumber, a.Manufacturer,
            a.Model, a.Location,
            a.AssignedUser == null ? null : new UserSummaryDto(a.AssignedUser.Id, a.AssignedUser.FullName, a.AssignedUser.Email, a.AssignedUser.Role),
            a.WarrantyEndDate,
            a.Tickets.Count(t => t.Status != TicketStatus.Closed)));
}

public sealed class UpsertAssetRequestValidator : AbstractValidator<UpsertAssetRequest>
{
    public UpsertAssetRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Cihaz adı zorunludur.").MaximumLength(120);
        RuleFor(x => x.InventoryNumber).NotEmpty().WithMessage("Envanter numarası zorunludur.").MaximumLength(50);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.Manufacturer).MaximumLength(100);
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.Location).MaximumLength(120);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.WarrantyEndDate).GreaterThanOrEqualTo(x => x.PurchaseDate)
            .When(x => x.PurchaseDate is not null && x.WarrantyEndDate is not null)
            .WithMessage("Garanti bitiş tarihi satın alma tarihinden önce olamaz.");
    }
}
