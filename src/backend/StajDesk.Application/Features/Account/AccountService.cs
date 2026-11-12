using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.Application.Features.Account;

/* =========================================================
   Oturumdaki kullanıcının hesap ayarları: tercihler, şifre değiştirme, SMS ile iki adımlı doğrulama.
   ========================================================= */

public sealed record SettingsDto(string Language, string Theme, bool EmailTicketUpdates, bool EmailAnnouncements, bool ShowBirthday = false);
public sealed record SecurityDto(bool SmsAvailable, string? SmsProvider, bool TwoFactorEnabled, string? MaskedPhone, bool HasPhone);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ConfirmOtpRequest(Guid ChallengeId, string Code);
public sealed record ConfirmPasswordRequest(string Password);
public sealed record OtpStartedDto(Guid ChallengeId, string MaskedPhone);

public interface IAccountService
{
    Task<SettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<SettingsDto> SaveSettingsAsync(SettingsDto settings, CancellationToken ct = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
    Task<SecurityDto> GetSecurityAsync(CancellationToken ct = default);
    Task<OtpStartedDto> StartTwoFactorAsync(CancellationToken ct = default);
    Task ConfirmTwoFactorAsync(ConfirmOtpRequest request, CancellationToken ct = default);
    Task DisableTwoFactorAsync(ConfirmPasswordRequest request, CancellationToken ct = default);
}

internal sealed class AccountService(IAppDbContext db, ICurrentUser currentUser, IPasswordHasher hasher, IAuditLogger audit, OtpService otp)
    : IAccountService
{
    public async Task<SettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var id = currentUser.RequireUserId();
        var s = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == id, ct) ?? new UserSettings();
        return new SettingsDto(s.Language, s.Theme, s.EmailTicketUpdates, s.EmailAnnouncements, s.ShowBirthday);
    }

    public async Task<SettingsDto> SaveSettingsAsync(SettingsDto r, CancellationToken ct = default)
    {
        var id = currentUser.RequireUserId();
        var s = await db.UserSettings.FirstOrDefaultAsync(x => x.UserId == id, ct);
        if (s is null) s = db.UserSettings.Add(new UserSettings { UserId = id }).Entity;
        s.Language = r.Language;
        s.Theme = r.Theme;
        s.EmailTicketUpdates = r.EmailTicketUpdates;
        s.EmailAnnouncements = r.EmailAnnouncements;
        s.ShowBirthday = r.ShowBirthday;
        await db.SaveChangesAsync(ct);
        return r;
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest r, CancellationToken ct = default)
    {
        var id = currentUser.RequireUserId();
        var u = await db.Users.FirstAsync(x => x.Id == id, ct);
        if (!hasher.Verify(u.PasswordHash, r.CurrentPassword)) throw new DomainException("Mevcut şifreniz hatalı.");
        if (r.CurrentPassword == r.NewPassword) throw new DomainException("Yeni şifre mevcut şifrenizden farklı olmalı.");
        u.PasswordHash = hasher.Hash(r.NewPassword);
        u.UpdatedAt = DateTime.UtcNow;
        audit.Add("user.password_changed", "user", id);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SecurityDto> GetSecurityAsync(CancellationToken ct = default)
    {
        var id = currentUser.RequireUserId();
        var u = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.TwoFactorEnabled, x.Phone }).FirstAsync(ct);
        var hasPhone = !string.IsNullOrWhiteSpace(u.Phone);
        return new SecurityDto(otp.IsAvailable, otp.IsAvailable ? otp.ProviderName : null, u.TwoFactorEnabled, hasPhone ? AccountLinks.MaskPhone(u.Phone!) : null, hasPhone);
    }

    /// <summary>İki adımlı doğrulamayı açmak için önce telefona kod gönderilir; kod doğrulanınca açılır (telefonun sahipliği kanıtlanır).</summary>
    public async Task<OtpStartedDto> StartTwoFactorAsync(CancellationToken ct = default)
    {
        var id = currentUser.RequireUserId();
        if (!otp.IsAvailable) throw new DomainException("SMS servisi yapılandırılmadığı için iki adımlı doğrulama şu anda açılamıyor. Sistem yöneticisine bildirin.");
        var u = await db.Users.FirstAsync(x => x.Id == id, ct);
        if (string.IsNullOrWhiteSpace(u.Phone)) throw new DomainException("Önce profilinizden cep telefonu numaranızı ekleyin.");
        var challenge = await otp.StartAsync(u, OtpPurpose.EnableTwoFactor, null, ct);
        return new OtpStartedDto(challenge, AccountLinks.MaskPhone(u.Phone));
    }

    public async Task ConfirmTwoFactorAsync(ConfirmOtpRequest r, CancellationToken ct = default)
    {
        var id = currentUser.RequireUserId();
        var c = await otp.ConsumeAsync(r.ChallengeId, r.Code, OtpPurpose.EnableTwoFactor, ct);
        if (c.UserId != id) throw new ForbiddenException();
        var u = await db.Users.FirstAsync(x => x.Id == id, ct);
        u.TwoFactorEnabled = true;
        audit.Add("user.two_factor_enabled", "user", id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Kapatmak için şifre tekrar istenir (açık bırakılmış oturumdan kapatılamasın).</summary>
    public async Task DisableTwoFactorAsync(ConfirmPasswordRequest confirm, CancellationToken ct = default)
    {
        var id = currentUser.RequireUserId();
        var u = await db.Users.FirstAsync(x => x.Id == id, ct);
        if (!hasher.Verify(u.PasswordHash, confirm.Password)) throw new DomainException("Şifreniz hatalı.");
        u.TwoFactorEnabled = false;
        audit.Add("user.two_factor_disabled", "user", id);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SettingsDtoValidator : AbstractValidator<SettingsDto>
{
    public SettingsDtoValidator()
    {
        RuleFor(x => x.Language).Must(l => l is "tr" or "en").WithMessage("Dil tr veya en olmalıdır.");
        RuleFor(x => x.Theme).Must(t => t is "system" or "light" or "dark").WithMessage("Tema system, light veya dark olmalıdır.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Mevcut şifrenizi girin.");
        RuleFor(x => x.NewPassword).SetValidator(new PasswordRule());
    }
}
