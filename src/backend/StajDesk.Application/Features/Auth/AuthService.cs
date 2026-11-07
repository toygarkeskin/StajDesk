using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.Application.Features.Auth;

public sealed record RegisterRequest(string FullName, string Email, string Password, string? Department);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto> MeAsync(CancellationToken ct = default);
}

internal sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    ILoginRateLimiter rateLimiter,
    ICurrentUser currentUser,
    ILogger<AuthService> logger) : IAuthService
{
    public const string InvalidCredentialsMessage = "E-posta ve şifre eşleşmedi. Bilgileri kontrol edip tekrar deneyin.";

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Bu e-posta adresiyle kayıtlı bir kullanıcı bulunmaktadır.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Department = request.Department?.Trim(),
            Role = UserRole.User,
            PasswordHash = hasher.Hash(request.Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Yeni kullanıcı kaydoldu: {UserId}", user.Id);
        var (token, expires) = tokens.CreateToken(user);
        return new AuthResponse(token, expires, user.ToDto());
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);

        var blockedFor = await rateLimiter.GetBlockedSecondsAsync(email, ct);
        if (blockedFor is not null)
        {
            logger.LogWarning("Giriş hız sınırına takıldı: {Email}", email);
            throw new TooManyRequestsException(
                "Bu hesap için çok sayıda başarısız giriş denemesi oldu. Güvenliğiniz için girişi birkaç dakika sonra tekrar deneyin.", blockedFor.Value);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.IsActive || !hasher.Verify(user.PasswordHash, request.Password))
        {
            await rateLimiter.RegisterFailureAsync(email, ct);
            logger.LogWarning("Başarısız giriş denemesi: {Email}", email);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        await rateLimiter.ResetAsync(email, ct);
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Kullanıcı giriş yaptı: {UserId} ({Role})", user.Id, user.Role);
        var (token, expires) = tokens.CreateToken(user);
        return new AuthResponse(token, expires, user.ToDto());
    }

    public async Task<UserDto> MeAsync(CancellationToken ct = default)
    {
        var id = currentUser.RequireUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new UnauthorizedException("Oturumunuz geçersiz.");
        return user.ToDto();
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.")
            .MaximumLength(100).WithMessage("Ad soyad en fazla 100 karakter olabilir.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
            .MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Şifre zorunludur.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
            .Matches("[A-Z]").WithMessage("Şifre en az bir büyük harf içermelidir.")
            .Matches("[a-z]").WithMessage("Şifre en az bir küçük harf içermelidir.")
            .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermelidir.");
        RuleFor(x => x.Department).MaximumLength(100);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta zorunludur.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Şifre zorunludur.");
    }
}
