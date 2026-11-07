using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Categories;

public sealed record CategoryDto(int Id, string Name, string? Description, string Color, bool IsActive, int TicketCount, int OpenTicketCount);
public sealed record UpsertCategoryRequest(string Name, string? Description, string Color, bool IsActive = true);

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(UpsertCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(int id, UpsertCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

internal sealed class CategoryService(IAppDbContext db, ICacheService cache, ICurrentUser currentUser, IAuditLogger audit, ILogger<CategoryService> logger) : ICategoryService
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.CategoriesRead, Permissions.TicketsCreate);
        // Cache-aside: önce Redis'e bak, yoksa veritabanından al ve 60 sn önbelleğe yaz.
        var all = await cache.GetOrSetAsync(CacheKeys.Categories, async token =>
            await db.Categories.AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Color, c.IsActive,
                    c.Tickets.Count, c.Tickets.Count(t => t.Status != Domain.Enums.TicketStatus.Closed)))
                .ToListAsync(token), CacheTtl, ct);

        return includeInactive && currentUser.Has(Permissions.CategoriesManage) ? all : all.Where(c => c.IsActive).ToList();
    }

    public async Task<CategoryDto> CreateAsync(UpsertCategoryRequest request, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.CategoriesManage);
        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Bu isimde bir kategori zaten mevcut.");

        var category = new Category { Name = name, Description = request.Description?.Trim(), Color = request.Color, IsActive = request.IsActive };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ct);
        logger.LogInformation("Kategori oluşturuldu: {CategoryId} {Name}", category.Id, category.Name);
        return new CategoryDto(category.Id, category.Name, category.Description, category.Color, category.IsActive, 0, 0);
    }

    public async Task<CategoryDto> UpdateAsync(int id, UpsertCategoryRequest request, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.CategoriesManage);
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFoundException.For("Kategori", id);
        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Bu isimde bir kategori zaten mevcut.");

        category.Name = name;
        category.Description = request.Description?.Trim();
        category.Color = request.Color;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ct);

        var counts = await db.Tickets.Where(t => t.CategoryId == id)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Open = g.Count(t => t.Status != Domain.Enums.TicketStatus.Closed) })
            .FirstOrDefaultAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Description, category.Color, category.IsActive,
            counts?.Total ?? 0, counts?.Open ?? 0);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.CategoriesManage);
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFoundException.For("Kategori", id);
        if (await db.Tickets.AnyAsync(t => t.CategoryId == id, ct))
            throw new ConflictException("Bu kategoriye bağlı talepler bulunduğu için silinemez. Bunun yerine pasif hâle getirebilirsiniz.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ct);
        logger.LogInformation("Kategori silindi: {CategoryId}", id);
    }

    // Kategori değişince ilgili önbellekler silinir (cache invalidation).
    private async Task InvalidateAsync(CancellationToken ct)
    {
        await cache.RemoveAsync(CacheKeys.Categories, ct);
        await cache.RemoveByPrefixAsync(CacheKeys.StatsPrefix, ct);
    }
}

public sealed class UpsertCategoryRequestValidator : AbstractValidator<UpsertCategoryRequest>
{
    private static readonly string[] Colors = ["violet", "sky", "emerald", "amber", "rose", "indigo", "teal", "orange", "slate", "pink"];

    public UpsertCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Kategori adı zorunludur.")
            .MaximumLength(80).WithMessage("Kategori adı en fazla 80 karakter olabilir.").NoMarkup();
        RuleFor(x => x.Description).MaximumLength(300).NoMarkup();
        RuleFor(x => x.Color).Must(c => Colors.Contains(c)).WithMessage("Geçersiz renk seçimi.");
    }
}
