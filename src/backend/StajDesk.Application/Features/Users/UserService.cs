using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.Application.Features.Users;

public sealed record UserDto(int Id, string FullName, string Email, UserRole Role, string? Department, string? Phone,
    bool IsActive, DateTime CreatedAt, DateTime? LastLoginAt);

public sealed record UserSummaryDto(int Id, string FullName, string Email, UserRole Role);

public sealed record UserListItemDto(int Id, string FullName, string Email, UserRole Role, string? Department,
    bool IsActive, DateTime CreatedAt, DateTime? LastLoginAt, int OpenTicketCount, int AssetCount);

public sealed record UserQuery : PagedQuery
{
    public UserRole? Role { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record CreateUserRequest(string FullName, string Email, string Password, UserRole Role, string? Department, string? Phone);
public sealed record UpdateUserRequest(string FullName, UserRole Role, string? Department, string? Phone, bool IsActive);

public static class UserMappings
{
    public static UserDto ToDto(this User u) =>
        new(u.Id, u.FullName, u.Email, u.Role, u.Department, u.Phone, u.IsActive, u.CreatedAt, u.LastLoginAt);
}

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetListAsync(UserQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<UserSummaryDto>> GetAgentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserSummaryDto>> LookupAsync(string? search, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default);
}

internal sealed class UserService(IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser, ILogger<UserService> logger)
    : IUserService
{
    public async Task<PagedResult<UserListItemDto>> GetListAsync(UserQuery query, CancellationToken ct = default)
    {
        var q = db.Users.AsNoTracking();
        if (query.Role is not null) q = q.Where(u => u.Role == query.Role);
        if (query.IsActive is not null) q = q.Where(u => u.IsActive == query.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(u => u.FullName.ToLower().Contains(s) || u.Email.Contains(s) ||
                             (u.Department != null && u.Department.ToLower().Contains(s)));
        }

        return await q.OrderBy(u => u.FullName)
            .Select(u => new UserListItemDto(u.Id, u.FullName, u.Email, u.Role, u.Department, u.IsActive, u.CreatedAt,
                u.LastLoginAt,
                u.RequestedTickets.Count(t => t.Status != TicketStatus.Closed),
                u.Assets.Count))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetAgentsAsync(CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => u.IsActive && (u.Role == UserRole.Agent || u.Role == UserRole.Admin))
            .OrderBy(u => u.FullName)
            .Select(u => new UserSummaryDto(u.Id, u.FullName, u.Email, u.Role))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserSummaryDto>> LookupAsync(string? search, CancellationToken ct = default)
    {
        var q = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(u => u.FullName.ToLower().Contains(s) || u.Email.Contains(s));
        }
        return await q.OrderBy(u => u.FullName).Take(20)
            .Select(u => new UserSummaryDto(u.Id, u.FullName, u.Email, u.Role)).ToListAsync(ct);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Bu e-posta adresiyle kayıtlı bir kullanıcı bulunmaktadır.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = request.Role,
            Department = request.Department?.Trim(),
            Phone = request.Phone?.Trim(),
            PasswordHash = hasher.Hash(request.Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Yönetici yeni kullanıcı oluşturdu: {UserId} ({Role})", user.Id, user.Role);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw NotFoundException.For("Kullanıcı", id);

        if (user.Id == currentUser.UserId && (request.Role != user.Role || !request.IsActive))
            throw new ForbiddenException("Kendi rolünüzü veya aktiflik durumunuzu değiştiremezsiniz.");

        if (user.Role != request.Role)
            logger.LogInformation("Kullanıcı rolü değişti: {UserId} {OldRole} -> {NewRole}", user.Id, user.Role, request.Role);

        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.Department = request.Department?.Trim();
        user.Phone = request.Phone?.Trim();
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.").MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.").MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.");
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Department).MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.").MaximumLength(100);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Department).MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}
