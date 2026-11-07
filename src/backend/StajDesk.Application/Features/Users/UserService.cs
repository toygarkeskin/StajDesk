using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Users;

public sealed record UserSummaryDto(int Id, string FullName, string Email);
public sealed record StoreRefDto(int Id, string Code, string Name);

/// <summary>Oturumdaki kullanıcı: arayüz menüleri ve ekranları bu izin listesine göre çizilir.</summary>
public sealed record MeDto(int Id, string FullName, string Email, string? Department, StoreRefDto? Store,
    IReadOnlyList<string> Roles, IReadOnlyList<string> RoleNames, IReadOnlyList<string> Permissions, DateTime CreatedAt);

public sealed record UserListItemDto(int Id, string FullName, string Email, string? Department, string? Phone, bool IsActive,
    StoreRefDto? Store, IReadOnlyList<string> Roles, DateTime CreatedAt, DateTime? LastLoginAt, int OpenTicketCount);

public sealed record UserQuery : PagedQuery
{
    public string? Role { get; init; }
    public int? StoreId { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record CreateUserRequest(string FullName, string Email, string Password, IReadOnlyList<string> Roles, int? StoreId, string? Department, string? Phone);
public sealed record UpdateUserRequest(string FullName, IReadOnlyList<string> Roles, int? StoreId, string? Department, string? Phone, bool IsActive);

public interface IUserService
{
    Task<MeDto> MeAsync(CancellationToken ct = default);
    Task<PagedResult<UserListItemDto>> GetListAsync(UserQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<UserSummaryDto>> GetAgentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserSummaryDto>> LookupAsync(string? search, CancellationToken ct = default);
    Task<int> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default);
}

internal sealed class UserService(IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser, ICacheService cache, IAuditLogger audit, ILogger<UserService> logger)
    : IUserService
{
    public async Task<MeDto> MeAsync(CancellationToken ct = default)
    {
        var id = currentUser.RequireUserId();
        var u = await db.Users.AsNoTracking().Include(x => x.Store).Include(x => x.Roles).ThenInclude(r => r.Role)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new UnauthorizedException("Oturumunuz geçersiz.");
        return new MeDto(u.Id, u.FullName, u.Email, u.Department,
            u.Store is null ? null : new StoreRefDto(u.Store.Id, u.Store.Code, u.Store.Name),
            u.Roles.Select(r => r.Role.DisplayName).ToList(), u.Roles.Select(r => r.Role.Name).ToList(),
            currentUser.Permissions.OrderBy(p => p).ToList(), u.CreatedAt);
    }

    /// <summary>Yönetici herkesi; mağaza sahibi yalnızca kendi mağazasındaki kullanıcıları görür.</summary>
    private IQueryable<User> VisibleUsers()
    {
        currentUser.Require(Permissions.UsersRead, Permissions.UsersManage);
        if (currentUser.HasAny(Permissions.UsersManage, Permissions.TicketsReadAll)) return db.Users;
        return currentUser.StoreId is { } storeId ? db.Users.Where(u => u.StoreId == storeId) : db.Users.Where(u => u.Id == currentUser.UserId);
    }

    public async Task<PagedResult<UserListItemDto>> GetListAsync(UserQuery query, CancellationToken ct = default)
    {
        var q = VisibleUsers().AsNoTracking();
        if (query.StoreId is not null) q = q.Where(u => u.StoreId == query.StoreId);
        if (query.IsActive is not null) q = q.Where(u => u.IsActive == query.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Role)) q = q.Where(u => u.Roles.Any(r => r.Role.Name == query.Role));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(u => u.FullName.ToLower().Contains(s) || u.Email.Contains(s) || (u.Department != null && u.Department.ToLower().Contains(s)));
        }

        return await q.OrderBy(u => u.FullName)
            .Select(u => new UserListItemDto(u.Id, u.FullName, u.Email, u.Department, u.Phone, u.IsActive,
                u.Store == null ? null : new StoreRefDto(u.Store.Id, u.Store.Code, u.Store.Name),
                u.Roles.Select(r => r.Role.Name).ToList(), u.CreatedAt, u.LastLoginAt,
                u.RequestedTickets.Count(t => t.Status != TicketStatus.Closed)))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetAgentsAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.TicketsManage);
        return await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role.Permissions.Any(p => p.Permission.Key == Permissions.TicketsManage)))
            .OrderBy(u => u.FullName)
            .Select(u => new UserSummaryDto(u.Id, u.FullName, u.Email))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> LookupAsync(string? search, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AssetsManage, Permissions.UsersManage, Permissions.TicketsManage);
        var q = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(u => u.FullName.ToLower().Contains(s) || u.Email.Contains(s));
        }
        return await q.OrderBy(u => u.FullName).Take(50).Select(u => new UserSummaryDto(u.Id, u.FullName, u.Email)).ToListAsync(ct);
    }

    public async Task<int> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.UsersManage);
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Bu e-posta adresiyle kayıtlı bir kullanıcı var.");
        await EnsureStoreAsync(request.StoreId, ct);

        var user = new User
        {
            FullName = Text.Clean(request.FullName), Email = email, PasswordHash = hasher.Hash(request.Password),
            StoreId = request.StoreId, Department = Text.CleanOrNull(request.Department), Phone = Text.CleanOrNull(request.Phone),
        };
        foreach (var role in await ResolveRolesAsync(request.Roles, ct)) user.Roles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        audit.Add("user.created", "user", user.Id, new { request.Roles });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Yönetici yeni kullanıcı oluşturdu: {UserId} ({Roles})", user.Id, string.Join(",", request.Roles));
        return user.Id;
    }

    public async Task UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.UsersManage);
        var user = await db.Users.Include(u => u.Roles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw NotFoundException.For("Kullanıcı", id);

        if (user.Id == currentUser.UserId &&
            (!request.IsActive || (user.Roles.Any(r => r.Role.Name == RoleNames.SystemAdmin) && !request.Roles.Contains(RoleNames.SystemAdmin))))
            throw new ForbiddenException("Kendi hesabınızı pasifleştiremez veya kendi yönetici rolünüzü kaldıramazsınız.");
        await EnsureStoreAsync(request.StoreId, ct);

        user.FullName = Text.Clean(request.FullName);
        user.StoreId = request.StoreId;
        user.Department = Text.CleanOrNull(request.Department);
        user.Phone = Text.CleanOrNull(request.Phone);
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.Roles.Clear();
        foreach (var role in await ResolveRolesAsync(request.Roles, ct)) user.Roles.Add(new UserRole { RoleId = role.Id, UserId = user.Id });
        audit.Add("user.updated", "user", user.Id, new { request.Roles, request.IsActive, request.StoreId });
        await db.SaveChangesAsync(ct);

        // İzin önbelleğini sil: değişiklik bir sonraki istekte hemen geçerli olsun.
        await cache.RemoveAsync(CacheKeys.Permissions(id), ct);
        if (!request.IsActive)
        {
            // Pasifleştirilen kullanıcının açık oturumları kapatılır.
            await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        }
    }

    private async Task<List<Role>> ResolveRolesAsync(IReadOnlyList<string> names, CancellationToken ct)
    {
        var roles = await db.Roles.Where(r => names.Contains(r.Name)).ToListAsync(ct);
        if (roles.Count != names.Distinct().Count()) throw new ConflictException("Seçilen rollerden biri bulunamadı.");
        return roles;
    }

    private async Task EnsureStoreAsync(int? storeId, CancellationToken ct)
    {
        if (storeId is not null && !await db.Stores.AnyAsync(s => s.Id == storeId, ct))
            throw NotFoundException.For("Mağaza", storeId);
    }
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.").MaximumLength(100).NoMarkup();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("ad@sirket.com biçiminde bir e-posta girin.").MaximumLength(150);
        RuleFor(x => x.Password).SetValidator(new PasswordRule());
        RuleFor(x => x.Roles).NotEmpty().WithMessage("En az bir rol seçin.");
        RuleFor(x => x.Department).MaximumLength(100).NoMarkup();
        RuleFor(x => x.Phone).MaximumLength(30).NoMarkup();
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.").MaximumLength(100).NoMarkup();
        RuleFor(x => x.Roles).NotEmpty().WithMessage("En az bir rol seçin.");
        RuleFor(x => x.Department).MaximumLength(100).NoMarkup();
        RuleFor(x => x.Phone).MaximumLength(30).NoMarkup();
    }
}

/// <summary>Şifre politikası: en az 10 karakter; büyük harf, küçük harf ve rakam.</summary>
public sealed class PasswordRule : AbstractValidator<string>
{
    public PasswordRule()
    {
        RuleFor(x => x).NotEmpty().WithMessage("Şifre zorunludur.")
            .MinimumLength(10).WithMessage("Şifre en az 10 karakter olmalıdır.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Şifreye en az bir büyük harf ekleyin.")
            .Matches("[a-z]").WithMessage("Şifreye en az bir küçük harf ekleyin.")
            .Matches("[0-9]").WithMessage("Şifreye en az bir rakam ekleyin.");
    }
}
