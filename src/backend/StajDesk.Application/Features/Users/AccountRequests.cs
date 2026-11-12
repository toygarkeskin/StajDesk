using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Account;
using StajDesk.Application.Features.Auth;
using StajDesk.Application.Features.Employees;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Users;

/* =========================================================
   Kurumsal hesap süreci (kapalı portal — kimse kendi hesabını açamaz):
     1) İK talep açar (accounts.request): yeni çalışan bilgileri veya ayrılan çalışan.
     2) BT / yönetici inceler (users.manage): onaylar → hesap açılır (kullanıcı adı + roller) ve
        "ilk şifreni belirle" daveti gider; ya da gerekçe yazarak reddeder.
     3) Çalışan davet bağlantısıyla kendi şifresini belirleyip giriş yapar.
   İK'nın personel yetkisi hesap açma / rol verme yetkisi DEĞİLDİR. Her adım denetim kaydına yazılır.
   ========================================================= */

public sealed record AccountRequestDto(
    int Id, AccountRequestType Type, AccountRequestStatus Status,
    string FullName, string? Email, string? EmployeeNumber,
    Employees.RefDto? Department, Employees.RefDto? Position, UserSummaryDto? Manager, StoreRefDto? Store,
    EmploymentType EmploymentType, DateOnly? HireDate, string? Phone,
    UserSummaryDto? TargetUser, DateOnly? TerminationDate, string? Note,
    UserSummaryDto RequestedBy, DateTime CreatedAt,
    UserSummaryDto? ReviewedBy, DateTime? ReviewedAt, string? ReviewNote, int? CreatedUserId);

public sealed record AccountRequestQuery : PagedQuery
{
    public AccountRequestStatus? Status { get; init; }
    public AccountRequestType? Type { get; init; }
}

public sealed record CreateAccountRequest(string FullName, string Email, string? EmployeeNumber, int? DepartmentId, int? PositionId, int? ManagerId,
    int? StoreId, EmploymentType EmploymentType, DateOnly? HireDate, string? Phone, string? Note);
public sealed record CreateDeactivationRequest(int UserId, DateOnly TerminationDate, string? Note);
public sealed record ApproveAccountRequest(string? Username, IReadOnlyList<string>? Roles, string? Note);
public sealed record RejectAccountRequest(string Reason);

public interface IAccountRequestService
{
    Task<PagedResult<AccountRequestDto>> ListAsync(AccountRequestQuery query, CancellationToken ct = default);
    Task<AccountRequestDto> GetAsync(int id, CancellationToken ct = default);
    Task<int> CreateNewAccountAsync(CreateAccountRequest request, CancellationToken ct = default);
    Task<int> CreateDeactivationAsync(CreateDeactivationRequest request, CancellationToken ct = default);
    Task CancelAsync(int id, CancellationToken ct = default);
    Task<AccountRequestDto> ApproveAsync(int id, ApproveAccountRequest request, CancellationToken ct = default);
    Task<AccountRequestDto> RejectAsync(int id, RejectAccountRequest request, CancellationToken ct = default);
    Task<int> PendingCountAsync(CancellationToken ct = default);
}

/// <summary>
/// Yetki yükseltme koruması: bir kişi yalnızca KENDİ sahip olduğu izinleri içeren rolleri atayabilir ve
/// kendisinden daha yetkili bir hesabı değiştiremez. Böylece BT hesap yöneticisi kendine veya başkasına
/// "Sistem Yöneticisi" rolü veremez.
/// </summary>
public static class RoleGuard
{
    public static async Task<List<Role>> ResolveAssignableAsync(IAppDbContext db, ICurrentUser actor, IReadOnlyList<string> roleNames, CancellationToken ct)
    {
        var roles = await db.Roles.Include(r => r.Permissions).ThenInclude(p => p.Permission).Where(r => roleNames.Contains(r.Name)).ToListAsync(ct);
        if (roles.Count != roleNames.Distinct().Count()) throw new ConflictException("Seçilen rollerden biri bulunamadı.");
        foreach (var role in roles)
        {
            var missing = role.Permissions.Select(p => p.Permission.Key).Where(k => !actor.Has(k)).ToList();
            if (missing.Count > 0)
                throw new ForbiddenException($"\"{role.DisplayName}\" rolü sizde olmayan yetkiler içeriyor ({string.Join(", ", missing.Take(3))}); bu rolü atayamazsınız.");
        }
        return roles;
    }

    /// <summary>Hedef hesabın yetkileri işlemi yapanın yetkilerini aşıyorsa (ör. BT → sistem yöneticisi) değişiklik reddedilir.</summary>
    public static async Task EnsureCanManageAsync(IAppDbContext db, ICurrentUser actor, int targetUserId, CancellationToken ct)
    {
        var targetPermissions = await db.UserRoles.Where(ur => ur.UserId == targetUserId)
            .SelectMany(ur => ur.Role.Permissions.Select(p => p.Permission.Key)).Distinct().ToListAsync(ct);
        if (targetPermissions.Any(p => !actor.Has(p)))
            throw new ForbiddenException("Bu hesap sizden daha geniş yetkilere sahip; değişikliği bir sistem yöneticisi yapmalıdır.");
    }
}

internal sealed class AccountRequestService(
    IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, IEventPublisher events, ICacheService cache,
    Microsoft.Extensions.Options.IOptions<AuthOptions> authOptions) : IAccountRequestService
{
    private bool CanReview => currentUser.Has(Permissions.UsersManage);

    private IQueryable<AccountRequest> Visible()
    {
        currentUser.Require(Permissions.AccountsRequest, Permissions.UsersManage);
        return db.AccountRequests.AsNoTracking();
    }

    private static readonly System.Linq.Expressions.Expression<Func<AccountRequest, AccountRequestDto>> Projection = r => new AccountRequestDto(
        r.Id, r.Type, r.Status, r.FullName, r.Email, r.EmployeeNumber,
        r.Department == null ? null : new Employees.RefDto(r.Department.Id, r.Department.Name),
        r.Position == null ? null : new Employees.RefDto(r.Position.Id, r.Position.Title),
        r.Manager == null ? null : new UserSummaryDto(r.Manager.Id, r.Manager.FullName, r.Manager.Email),
        r.Store == null ? null : new StoreRefDto(r.Store.Id, r.Store.Code, r.Store.Name),
        r.EmploymentType, r.HireDate, r.Phone,
        r.TargetUser == null ? null : new UserSummaryDto(r.TargetUser.Id, r.TargetUser.FullName, r.TargetUser.Email),
        r.TerminationDate, r.Note,
        new UserSummaryDto(r.RequestedBy.Id, r.RequestedBy.FullName, r.RequestedBy.Email), r.CreatedAt,
        r.ReviewedBy == null ? null : new UserSummaryDto(r.ReviewedBy.Id, r.ReviewedBy.FullName, r.ReviewedBy.Email),
        r.ReviewedAt, r.ReviewNote, r.CreatedUserId);

    public async Task<PagedResult<AccountRequestDto>> ListAsync(AccountRequestQuery query, CancellationToken ct = default)
    {
        var q = Visible();
        if (query.Status is not null) q = q.Where(r => r.Status == query.Status);
        if (query.Type is not null) q = q.Where(r => r.Type == query.Type);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(r => r.FullName.ToLower().Contains(s) || (r.Email != null && r.Email.Contains(s)) || r.EmployeeNumber == s);
        }
        // Bekleyenler önce, sonra en yeni
        return await q.OrderBy(r => r.Status == AccountRequestStatus.Pending ? 0 : 1).ThenByDescending(r => r.CreatedAt).Select(Projection).ToPagedResultAsync(query, ct);
    }

    public async Task<AccountRequestDto> GetAsync(int id, CancellationToken ct = default) =>
        await Visible().Where(r => r.Id == id).Select(Projection).FirstOrDefaultAsync(ct) ?? throw NotFoundException.For("Hesap talebi", id);

    public async Task<int> PendingCountAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AccountsRequest, Permissions.UsersManage);
        return await db.AccountRequests.CountAsync(r => r.Status == AccountRequestStatus.Pending, ct);
    }

    public async Task<int> CreateNewAccountAsync(CreateAccountRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AccountsRequest, Permissions.UsersManage);
        var userId = currentUser.RequireUserId();
        var email = r.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) throw new ConflictException("Bu kurumsal e-posta adresiyle bir hesap zaten var.");
        if (await db.AccountRequests.AnyAsync(x => x.Email == email && x.Status == AccountRequestStatus.Pending && x.Type == AccountRequestType.NewAccount, ct))
            throw new ConflictException("Bu e-posta için bekleyen bir hesap talebi zaten var.");
        var number = Text.CleanOrNull(r.EmployeeNumber);
        if (number is not null && (await db.EmployeeProfiles.AnyAsync(p => p.EmployeeNumber == number, ct)
                                   || await db.AccountRequests.AnyAsync(x => x.EmployeeNumber == number && x.Status == AccountRequestStatus.Pending, ct)))
            throw new ConflictException($"{number} personel kodu başka bir çalışana veya bekleyen talebe ait.");
        if (r.DepartmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d && x.IsActive, ct)) throw NotFoundException.For("Departman", d);
        if (r.PositionId is { } p && !await db.Positions.AnyAsync(x => x.Id == p, ct)) throw NotFoundException.For("Unvan", p);
        if (r.ManagerId is { } m && !await db.Users.AnyAsync(x => x.Id == m && x.IsActive, ct)) throw NotFoundException.For("Yönetici", m);
        if (r.StoreId is { } s && !await db.Stores.AnyAsync(x => x.Id == s, ct)) throw NotFoundException.For("Mağaza", s);

        var req = new AccountRequest
        {
            Type = AccountRequestType.NewAccount, FullName = Text.Clean(r.FullName), Email = email, EmployeeNumber = number,
            DepartmentId = r.DepartmentId, PositionId = r.PositionId, ManagerId = r.ManagerId, StoreId = r.StoreId,
            EmploymentType = r.EmploymentType, HireDate = r.HireDate, Phone = Text.CleanOrNull(r.Phone), Note = Text.CleanOrNull(r.Note),
            RequestedById = userId,
        };
        db.AccountRequests.Add(req);
        await db.SaveChangesAsync(ct);
        await NotifyReviewersAsync($"Yeni hesap talebi: {req.FullName}", $"{currentUser.FullName} yeni çalışan için portal hesabı istedi.", ct);
        audit.Add("account_request.created", "account_request", req.Id, new { type = "NewAccount", req.DepartmentId, req.PositionId });
        await db.SaveChangesAsync(ct);
        return req.Id;
    }

    public async Task<int> CreateDeactivationAsync(CreateDeactivationRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AccountsRequest, Permissions.UsersManage);
        var userId = currentUser.RequireUserId();
        var target = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == r.UserId, ct) ?? throw NotFoundException.For("Çalışan", r.UserId);
        if (target.Id == userId) throw new DomainException("Kendi hesabınız için kapatma talebi açamazsınız.");
        if (!target.IsActive) throw new DomainException("Bu çalışanın hesabı zaten kapalı.");
        if (await db.AccountRequests.AnyAsync(x => x.TargetUserId == r.UserId && x.Status == AccountRequestStatus.Pending, ct))
            throw new ConflictException("Bu çalışan için bekleyen bir kapatma talebi zaten var.");

        var req = new AccountRequest
        {
            Type = AccountRequestType.Deactivation, FullName = target.FullName, TargetUserId = target.Id, TerminationDate = r.TerminationDate,
            Note = Text.CleanOrNull(r.Note), RequestedById = userId,
        };
        db.AccountRequests.Add(req);
        await db.SaveChangesAsync(ct);
        await NotifyReviewersAsync($"Hesap kapatma talebi: {target.FullName}", $"{currentUser.FullName}, {r.TerminationDate:dd.MM.yyyy} tarihinde ayrılan çalışanın hesabının kapatılmasını istedi.", ct);
        audit.Add("account_request.created", "account_request", req.Id, new { type = "Deactivation", r.UserId, r.TerminationDate });
        await db.SaveChangesAsync(ct);
        return req.Id;
    }

    public async Task CancelAsync(int id, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var req = await LoadPendingAsync(id, ct);
        if (req.RequestedById != userId && !CanReview) throw new ForbiddenException("Talebi yalnızca açan kişi geri çekebilir.");
        req.Status = AccountRequestStatus.Cancelled;
        req.ReviewedAt = DateTime.UtcNow;
        audit.Add("account_request.cancelled", "account_request", id);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AccountRequestDto> ApproveAsync(int id, ApproveAccountRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.UsersManage);
        var reviewerId = currentUser.RequireUserId();
        var req = await LoadPendingAsync(id, ct);
        if (req.RequestedById == reviewerId && !currentUser.Has(Permissions.RolesManage))
            throw new ForbiddenException("Kendi açtığınız talebi onaylayamazsınız (görevler ayrılığı). Başka bir BT yetkilisi onaylamalı.");

        if (req.Type == AccountRequestType.NewAccount) await CreateAccountAsync(req, r, ct);
        else await DeactivateAsync(req, ct);

        req.Status = AccountRequestStatus.Approved;
        req.ReviewedById = reviewerId;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = Text.CleanOrNull(r.Note);
        Notify(req.RequestedById, $"Hesap talebi onaylandı: {req.FullName}",
            req.Type == AccountRequestType.NewAccount ? "Hesap açıldı; çalışana ilk şifre bağlantısı e-postayla gönderildi." : "Çalışanın hesabı kapatıldı.");
        audit.Add("account_request.approved", "account_request", id, new { type = req.Type.ToString(), req.CreatedUserId, req.TargetUserId });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task CreateAccountAsync(AccountRequest req, ApproveAccountRequest r, CancellationToken ct)
    {
        var email = req.Email!;
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) throw new ConflictException("Bu e-posta ile bir hesap bu arada açılmış. Talebi reddedin.");
        var username = EmployeeService.NormalizeUsername(r.Username);
        if (username is not null && await db.Users.AnyAsync(u => u.Username == username, ct)) throw new ConflictException("Bu kullanıcı adı kullanılıyor.");
        var roleNames = r.Roles is { Count: > 0 } ? r.Roles : [RoleNames.User];
        var roles = await RoleGuard.ResolveAssignableAsync(db, currentUser, roleNames, ct);

        var user = new User
        {
            FullName = req.FullName, Email = email, Username = username, Phone = req.Phone,
            DepartmentId = req.DepartmentId, PositionId = req.PositionId, ManagerId = req.ManagerId, StoreId = req.StoreId,
            PasswordHash = string.Empty, // çalışan davet bağlantısıyla kendisi belirler; boş hash ile giriş yapılamaz
            Profile = new EmployeeProfile { EmployeeNumber = req.EmployeeNumber, HireDate = req.HireDate, EmploymentType = req.EmploymentType },
        };
        foreach (var role in roles) user.Roles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        AccountLinks.Invite(db, events, authOptions.Value.WebUrl, user, currentUser.FullName ?? "Bilgi Teknolojileri");
        req.CreatedUserId = user.Id;
        audit.Add("user.created", "user", user.Id, new { roles = roleNames, fromRequest = req.Id });
    }

    private async Task DeactivateAsync(AccountRequest req, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == req.TargetUserId, ct) ?? throw NotFoundException.For("Çalışan", req.TargetUserId);
        await RoleGuard.EnsureCanManageAsync(db, currentUser, user.Id, ct);
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        user.Profile ??= new EmployeeProfile { UserId = user.Id };
        user.Profile.TerminationDate = req.TerminationDate;
        foreach (var t in await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct)) t.RevokedAt = DateTime.UtcNow;
        await cache.RemoveAsync(CacheKeys.Permissions(user.Id), ct);
        audit.Add("user.deactivated", "user", user.Id, new { fromRequest = req.Id, req.TerminationDate });
    }

    public async Task<AccountRequestDto> RejectAsync(int id, RejectAccountRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.UsersManage);
        var req = await LoadPendingAsync(id, ct);
        req.Status = AccountRequestStatus.Rejected;
        req.ReviewedById = currentUser.RequireUserId();
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = Text.Clean(r.Reason);
        Notify(req.RequestedById, $"Hesap talebi reddedildi: {req.FullName}", $"Gerekçe: {req.ReviewNote}");
        audit.Add("account_request.rejected", "account_request", id, new { reason = req.ReviewNote });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<AccountRequest> LoadPendingAsync(int id, CancellationToken ct)
    {
        currentUser.Require(Permissions.AccountsRequest, Permissions.UsersManage);
        var req = await db.AccountRequests.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw NotFoundException.For("Hesap talebi", id);
        if (req.Status != AccountRequestStatus.Pending) throw new DomainException("Bu talep zaten sonuçlanmış.");
        return req;
    }

    private void Notify(int userId, string title, string message) =>
        db.Notifications.Add(new Notification { UserId = userId, Title = title.Length > 200 ? title[..199] : title, Message = message.Length > 1000 ? message[..999] : message });

    /// <summary>Hesap taleplerini inceleyebilen herkese (users.manage) uygulama içi bildirim.</summary>
    private async Task NotifyReviewersAsync(string title, string message, CancellationToken ct)
    {
        var reviewers = await db.Users.Where(u => u.IsActive && u.LockedAt == null && u.Id != currentUser.UserId &&
                u.Roles.Any(r => r.Role.Permissions.Any(p => p.Permission.Key == Permissions.UsersManage)))
            .Select(u => u.Id).ToListAsync(ct);
        foreach (var id in reviewers) Notify(id, title, message);
    }
}

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.").MaximumLength(100).NoMarkup();
        RuleFor(x => x.Email).NotEmpty().WithMessage("Kurumsal e-posta zorunludur.").EmailAddress().WithMessage("ad@sirket.com biçiminde bir e-posta girin.").MaximumLength(150);
        RuleFor(x => x.EmployeeNumber).NotEmpty().WithMessage("Personel kodu zorunludur.").MaximumLength(20).NoMarkup();
        RuleFor(x => x.DepartmentId).NotNull().WithMessage("Departman seçin.");
        RuleFor(x => x.PositionId).NotNull().WithMessage("Unvan seçin.");
        RuleFor(x => x.HireDate).NotNull().WithMessage("İşe başlama tarihini girin.");
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.Phone).MaximumLength(30).NoMarkup();
        RuleFor(x => x.Note).MaximumLength(1000).NoMarkup();
    }
}

public sealed class CreateDeactivationRequestValidator : AbstractValidator<CreateDeactivationRequest>
{
    public CreateDeactivationRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("Çalışanı seçin.");
        RuleFor(x => x.Note).MaximumLength(1000).NoMarkup();
    }
}

public sealed class ApproveAccountRequestValidator : AbstractValidator<ApproveAccountRequest>
{
    public ApproveAccountRequestValidator()
    {
        RuleFor(x => x.Username).Matches("^[a-zA-Z0-9._-]{3,50}$").WithMessage("Kullanıcı adı 3–50 karakter olmalı; harf, rakam, nokta, tire ve alt çizgi içerebilir.")
            .When(x => !string.IsNullOrWhiteSpace(x.Username));
        RuleFor(x => x.Note).MaximumLength(1000).NoMarkup();
    }
}

public sealed class RejectAccountRequestValidator : AbstractValidator<RejectAccountRequest>
{
    public RejectAccountRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Reddetme gerekçesini yazın; talep sahibine iletilir.").MaximumLength(1000).NoMarkup();
}
