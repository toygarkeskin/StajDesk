using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Users;

public sealed record UserSummaryDto(int Id, string FullName, string Email);
public sealed record StoreRefDto(int Id, string Code, string Name);

/// <summary>Oturumdaki kullanıcı: arayüz menüleri ve ekranları bu izin listesine göre çizilir.</summary>
public sealed record MeDto(int Id, string FullName, string Email, string? Department, StoreRefDto? Store,
    IReadOnlyList<string> Roles, IReadOnlyList<string> RoleNames, IReadOnlyList<string> Permissions, DateTime CreatedAt,
    string? Username = null, string? Title = null, string? PhotoVersion = null, string Language = "tr", string Theme = "system");

public sealed record UserListItemDto(int Id, string FullName, string Email, string? Department, string? Phone, bool IsActive,
    StoreRefDto? Store, IReadOnlyList<string> Roles, DateTime CreatedAt, DateTime? LastLoginAt, int OpenTicketCount,
    int? DepartmentId = null, string? Username = null, bool PasswordSet = true, DateTime? LockedAt = null, string? LockReason = null);

public sealed record UserQuery : PagedQuery
{
    public string? Role { get; init; }
    public int? StoreId { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>BT / yöneticinin doğrudan açtığı hesap. Şifre hiçbir zaman başkası tarafından belirlenmez: çalışana "ilk şifreni belirle" daveti gider.</summary>
public sealed record CreateUserRequest(string FullName, string Email, IReadOnlyList<string> Roles, int? StoreId, int? DepartmentId, string? Phone, string? Username = null);
public sealed record LockUserRequest(string Reason);
public sealed record UpdateUserRequest(string FullName, IReadOnlyList<string> Roles, int? StoreId, int? DepartmentId, string? Phone, bool IsActive, string? Username = null);

public interface IUserService
{
    Task<MeDto> MeAsync(CancellationToken ct = default);
    Task<PagedResult<UserListItemDto>> GetListAsync(UserQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<UserSummaryDto>> GetAgentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserSummaryDto>> LookupAsync(string? search, CancellationToken ct = default);
    Task<int> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default);
    Task LockAsync(int id, LockUserRequest request, CancellationToken ct = default);
    Task UnlockAsync(int id, CancellationToken ct = default);
    Task ResendInvitationAsync(int id, CancellationToken ct = default);
}

internal sealed class UserService(IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser, ICacheService cache, IAuditLogger audit, ILogger<UserService> logger,
    IEventPublisher events, Microsoft.Extensions.Options.IOptions<Auth.AuthOptions> authOptions)
    : IUserService
{
    public async Task<MeDto> MeAsync(CancellationToken ct = default)
    {
        var id = currentUser.RequireUserId();
        var u = await db.Users.AsNoTracking().Include(x => x.Store).Include(x => x.Department).Include(x => x.Position)
                    .Include(x => x.Profile).Include(x => x.Settings).Include(x => x.Roles).ThenInclude(r => r.Role)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new UnauthorizedException("Oturumunuz geçersiz.");
        return new MeDto(u.Id, u.FullName, u.Email, u.Department?.Name,
            u.Store is null ? null : new StoreRefDto(u.Store.Id, u.Store.Code, u.Store.Name),
            u.Roles.Select(r => r.Role.DisplayName).ToList(), u.Roles.Select(r => r.Role.Name).ToList(),
            currentUser.Permissions.OrderBy(p => p).ToList(), u.CreatedAt,
            u.Username, u.Position?.Title, u.Profile?.PhotoKey, u.Settings?.Language ?? "tr", u.Settings?.Theme ?? "system");
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
            q = q.Where(u => u.FullName.ToLower().Contains(s) || u.Email.Contains(s) || (u.Department != null && u.Department.Name.ToLower().Contains(s)));
        }

        return await q.OrderBy(u => u.FullName)
            .Select(u => new UserListItemDto(u.Id, u.FullName, u.Email, u.Department == null ? null : u.Department.Name, u.Phone, u.IsActive,
                u.Store == null ? null : new StoreRefDto(u.Store.Id, u.Store.Code, u.Store.Name),
                u.Roles.Select(r => r.Role.Name).ToList(), u.CreatedAt, u.LastLoginAt,
                u.RequestedTickets.Count(t => t.Status != TicketStatus.Closed), u.DepartmentId, u.Username, u.PasswordHash != "", u.LockedAt, u.LockReason))
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
        var username = Employees.EmployeeService.NormalizeUsername(request.Username);
        if (username is not null && await db.Users.AnyAsync(u => u.Username == username, ct)) throw new ConflictException("Bu kullanıcı adı kullanılıyor.");
        await EnsureStoreAsync(request.StoreId, ct);
        await EnsureDepartmentAsync(request.DepartmentId, ct);

        var roles = await RoleGuard.ResolveAssignableAsync(db, currentUser, request.Roles, ct);
        var user = new User
        {
            FullName = Text.Clean(request.FullName), Email = email, Username = username,
            PasswordHash = string.Empty, // çalışan davet bağlantısıyla kendisi belirler
            StoreId = request.StoreId, DepartmentId = request.DepartmentId, Phone = Text.CleanOrNull(request.Phone),
            Profile = new EmployeeProfile(),
        };
        foreach (var role in roles) user.Roles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        Account.AccountLinks.Invite(db, events, authOptions.Value.WebUrl, user, currentUser.FullName ?? "Bilgi Teknolojileri");
        audit.Add("user.created", "user", user.Id, new { request.Roles, direct = true });
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
        await RoleGuard.EnsureCanManageAsync(db, currentUser, id, ct);
        var newRoles = await RoleGuard.ResolveAssignableAsync(db, currentUser, request.Roles, ct);
        await EnsureStoreAsync(request.StoreId, ct);
        await EnsureDepartmentAsync(request.DepartmentId, ct);
        var username = Employees.EmployeeService.NormalizeUsername(request.Username);
        if (username is not null && await db.Users.AnyAsync(u => u.Username == username && u.Id != id, ct)) throw new ConflictException("Bu kullanıcı adı kullanılıyor.");

        user.FullName = Text.Clean(request.FullName);
        user.Username = username;
        user.StoreId = request.StoreId;
        user.DepartmentId = request.DepartmentId;
        user.Phone = Text.CleanOrNull(request.Phone);
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.Roles.Clear();
        foreach (var role in newRoles) user.Roles.Add(new UserRole { RoleId = role.Id, UserId = user.Id });
        audit.Add("user.updated", "user", user.Id, new { request.Roles, request.IsActive, request.StoreId });
        await db.SaveChangesAsync(ct);

        // İzin önbelleğini sil: değişiklik bir sonraki istekte hemen geçerli olsun.
        await cache.RemoveAsync(CacheKeys.Permissions(id), ct);
        if (!request.IsActive)
        {
            // Pasifleştirilen kullanıcının açık oturumları kapatılır.
            foreach (var t in await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync(ct)) t.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Hesabı kilitler: şifre doğru olsa da giriş yapılamaz, açık oturumlar hemen kapanır (ör. hesabın ele geçirildiğinden şüphelenildiğinde).</summary>
    public async Task LockAsync(int id, LockUserRequest request, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.UsersManage);
        if (id == currentUser.UserId) throw new ForbiddenException("Kendi hesabınızı kilitleyemezsiniz.");
        await RoleGuard.EnsureCanManageAsync(db, currentUser, id, ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw NotFoundException.For("Kullanıcı", id);
        if (user.LockedAt is not null) throw new DomainException("Hesap zaten kilitli.");
        user.LockedAt = DateTime.UtcNow;
        user.LockReason = Text.Clean(request.Reason);
        foreach (var t in await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync(ct)) t.RevokedAt = DateTime.UtcNow;
        audit.Add("user.locked", "user", id, new { reason = user.LockReason });
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Permissions(id), ct);
    }

    public async Task UnlockAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.UsersManage);
        await RoleGuard.EnsureCanManageAsync(db, currentUser, id, ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw NotFoundException.For("Kullanıcı", id);
        if (user.LockedAt is null) throw new DomainException("Hesap kilitli değil.");
        user.LockedAt = null;
        user.LockReason = null;
        audit.Add("user.unlocked", "user", id);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Permissions(id), ct);
    }

    /// <summary>Davet bağlantısının süresi dolduysa veya e-posta kaybolduysa yenisi gönderilir (yalnızca şifresini henüz belirlememiş hesaplar).</summary>
    public async Task ResendInvitationAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.UsersManage);
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct) ?? throw NotFoundException.For("Kullanıcı", id);
        if (!string.IsNullOrEmpty(u.PasswordHash)) throw new DomainException("Bu çalışan şifresini zaten belirlemiş. Şifresini unuttuysa giriş ekranından sıfırlayabilir.");
        Account.AccountLinks.Invite(db, events, authOptions.Value.WebUrl, u, currentUser.FullName ?? "Bilgi Teknolojileri");
        audit.Add("user.invitation_resent", "user", id);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureDepartmentAsync(int? departmentId, CancellationToken ct)
    {
        if (departmentId is not null && !await db.Departments.AnyAsync(d => d.Id == departmentId && d.IsActive, ct))
            throw NotFoundException.For("Departman", departmentId);
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
        RuleFor(x => x.Roles).NotEmpty().WithMessage("En az bir rol seçin.");
        RuleFor(x => x.Username).Matches("^[a-zA-Z0-9._-]{3,50}$").WithMessage("Kullanıcı adı 3–50 karakter olmalı; harf, rakam, nokta, tire ve alt çizgi içerebilir.").When(x => !string.IsNullOrWhiteSpace(x.Username));
        RuleFor(x => x.Phone).MaximumLength(30).NoMarkup();
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.").MaximumLength(100).NoMarkup();
        RuleFor(x => x.Roles).NotEmpty().WithMessage("En az bir rol seçin.");
        RuleFor(x => x.Username).Matches("^[a-zA-Z0-9._-]{3,50}$").WithMessage("Kullanıcı adı 3–50 karakter olmalı; harf, rakam, nokta, tire ve alt çizgi içerebilir.").When(x => !string.IsNullOrWhiteSpace(x.Username));
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

public sealed class LockUserRequestValidator : AbstractValidator<LockUserRequest>
{
    public LockUserRequestValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Kilitleme gerekçesini yazın.").MaximumLength(300).NoMarkup();
}
