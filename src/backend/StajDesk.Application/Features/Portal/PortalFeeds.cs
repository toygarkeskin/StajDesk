using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Tickets;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Portal;

/* =========================================================
   Giriş sonrası portalın kurumsal akışları ve genel arama.
   Bu veriler yalnızca oturum açmış çalışanlara açıktır (kurumsal sitede gösterilmez — KVKK kararı).
   ========================================================= */

public sealed record PersonRefDto(int Id, string FullName, string? Title, string? Department, bool HasPhoto, string? PhotoVersion);
public sealed record BirthdayDto(PersonRefDto Person, int Month, int Day, bool IsToday);
public sealed record NewHireDto(PersonRefDto Person, DateOnly HireDate);
public sealed record CompanyEventDto(int Id, string Title, string? Description, string? Location, DateTime StartsAt, DateTime? EndsAt, bool IsPublished);
public sealed record RecognitionDto(int Id, RecognitionType Type, string Title, string? Description, DateOnly Date, PersonRefDto Person);
public sealed record UpsertCompanyEventRequest(string Title, string? Description, string? Location, DateTime StartsAt, DateTime? EndsAt, bool IsPublished);
public sealed record UpsertRecognitionRequest(int UserId, RecognitionType Type, string Title, string? Description, DateOnly Date);

public sealed record SearchPersonDto(int Id, string FullName, string? Title, string? Department, bool HasPhoto, string? PhotoVersion);
public sealed record SearchRefDto(int Id, string Title, string? Subtitle);
public sealed record SearchResultDto(IReadOnlyList<SearchPersonDto> People, IReadOnlyList<SearchRefDto> Departments,
    IReadOnlyList<SearchRefDto> Announcements, IReadOnlyList<SearchRefDto> Tickets);

public interface IPortalService
{
    Task<IReadOnlyList<BirthdayDto>> BirthdaysAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NewHireDto>> NewHiresAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CompanyEventDto>> UpcomingEventsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RecognitionDto>> RecentRecognitionsAsync(CancellationToken ct = default);
    Task<SearchResultDto> SearchAsync(string term, CancellationToken ct = default);

    // Yönetim (content.manage; takdirler için İK da)
    Task<IReadOnlyList<CompanyEventDto>> AllEventsAsync(CancellationToken ct = default);
    Task<CompanyEventDto> SaveEventAsync(int? id, UpsertCompanyEventRequest request, CancellationToken ct = default);
    Task DeleteEventAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<RecognitionDto>> AllRecognitionsAsync(CancellationToken ct = default);
    Task<RecognitionDto> SaveRecognitionAsync(int? id, UpsertRecognitionRequest request, CancellationToken ct = default);
    Task DeleteRecognitionAsync(int id, CancellationToken ct = default);
}

internal sealed class PortalService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, TimeProvider clock) : IPortalService
{
    private static readonly TimeZoneInfo Zone = FindZone();
    private static TimeZoneInfo FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "Türkiye", "Türkiye"); }
    }
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Now, Zone));

    private static readonly System.Linq.Expressions.Expression<Func<User, PersonRefDto>> Person = u => new PersonRefDto(
        u.Id, u.FullName, u.Position == null ? null : u.Position.Title, u.Department == null ? null : u.Department.Name,
        u.Profile != null && u.Profile.PhotoKey != null, u.Profile == null ? null : u.Profile.PhotoKey);

    /// <summary>Rehberde görünen çalışanlar (EmployeeService.ListAsync ile aynı kural).</summary>
    private IQueryable<User> Directory()
    {
        var q = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!currentUser.Has(Permissions.EmployeesManage)) q = q.Where(u => u.EmailVerifiedAt != null || u.PasswordHash == "");
        return q;
    }

    private bool CanManageContent => currentUser.Has(Permissions.ContentManage);

    // ---------- Akışlar ----------

    /// <summary>Önümüzdeki 7 gün. Yalnızca "doğum günüm görünsün" diyen çalışanlar; yıl (yaş) hiç dönmez.</summary>
    public async Task<IReadOnlyList<BirthdayDto>> BirthdaysAsync(CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var optedIn = db.UserSettings.Where(s => s.ShowBirthday).Select(s => s.UserId);
        var rows = await Directory().Where(u => optedIn.Contains(u.Id) && u.Profile != null && u.Profile.BirthDate != null)
            .Select(u => new { P = new PersonRefDto(u.Id, u.FullName, u.Position == null ? null : u.Position.Title, u.Department == null ? null : u.Department.Name,
                u.Profile!.PhotoKey != null, u.Profile.PhotoKey), Birth = u.Profile.BirthDate!.Value })
            .ToListAsync(ct);
        var today = Today;
        int DaysUntil(DateOnly birth)
        {
            for (var i = 0; i <= 7; i++)
            {
                var d = today.AddDays(i);
                if (d.Month == birth.Month && (d.Day == birth.Day || (birth.Month == 2 && birth.Day == 29 && d.Day == 28 && !DateTime.IsLeapYear(d.Year)))) return i;
            }
            return -1;
        }
        return rows.Select(r => (r, days: DaysUntil(r.Birth))).Where(x => x.days >= 0).OrderBy(x => x.days).ThenBy(x => x.r.P.FullName).Take(20)
            .Select(x => new BirthdayDto(x.r.P, x.r.Birth.Month, x.r.Birth.Day, x.days == 0)).ToList();
    }

    public async Task<IReadOnlyList<NewHireDto>> NewHiresAsync(CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var today = Today;
        var since = today.AddDays(-30);
        return await Directory().Where(u => u.Profile != null && u.Profile.HireDate >= since && u.Profile.HireDate <= today)
            .OrderByDescending(u => u.Profile!.HireDate).ThenBy(u => u.FullName).Take(12)
            .Select(u => new NewHireDto(new PersonRefDto(u.Id, u.FullName, u.Position == null ? null : u.Position.Title, u.Department == null ? null : u.Department.Name,
                u.Profile!.PhotoKey != null, u.Profile.PhotoKey), u.Profile.HireDate!.Value))
            .ToListAsync(ct);
    }

    private static CompanyEventDto ToDto(CompanyEvent e) => new(e.Id, e.Title, e.Description, e.Location, e.StartsAt, e.EndsAt, e.IsPublished);

    public async Task<IReadOnlyList<CompanyEventDto>> UpcomingEventsAsync(CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var now = Now;
        var rows = await db.CompanyEvents.AsNoTracking().Where(e => e.IsPublished && (e.EndsAt ?? e.StartsAt) >= now)
            .OrderBy(e => e.StartsAt).Take(8).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<RecognitionDto>> RecentRecognitionsAsync(CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var since = Today.AddDays(-60);
        var visible = Directory().Select(u => u.Id);
        return await db.Recognitions.AsNoTracking().Where(r => r.Date >= since && visible.Contains(r.UserId))
            .OrderByDescending(r => r.Type == RecognitionType.EmployeeOfWeek).ThenByDescending(r => r.Date).Take(8)
            .Select(r => new RecognitionDto(r.Id, r.Type, r.Title, r.Description, r.Date,
                new PersonRefDto(r.User.Id, r.User.FullName, r.User.Position == null ? null : r.User.Position.Title, r.User.Department == null ? null : r.User.Department.Name,
                    r.User.Profile != null && r.User.Profile.PhotoKey != null, r.User.Profile == null ? null : r.User.Profile.PhotoKey)))
            .ToListAsync(ct);
    }

    // ---------- Genel arama ----------

    /// <summary>Kişi, departman, duyuru ve talep arar. Her grup kullanıcının zaten görebildiği verilerle sınırlıdır.</summary>
    public async Task<SearchResultDto> SearchAsync(string term, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var s = (term ?? "").Trim();
        if (s.Length < 2) return new SearchResultDto([], [], [], []);
        if (s.Length > 100) s = s[..100];
        var lower = s.ToLower();

        var people = await Directory()
            .Where(u => u.FullName.ToLower().Contains(lower) || u.Email.Contains(lower) || (u.Position != null && u.Position.Title.ToLower().Contains(lower))
                        || (u.Profile != null && u.Profile.EmployeeNumber == s))
            .OrderBy(u => u.FullName).Take(6)
            .Select(u => new SearchPersonDto(u.Id, u.FullName, u.Position == null ? null : u.Position.Title, u.Department == null ? null : u.Department.Name,
                u.Profile != null && u.Profile.PhotoKey != null, u.Profile == null ? null : u.Profile.PhotoKey))
            .ToListAsync(ct);

        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive && (d.Name.ToLower().Contains(lower) || d.Code.ToLower() == lower))
            .OrderBy(d => d.Name).Take(5)
            .Select(d => new SearchRefDto(d.Id, d.Name, d.Parent == null ? null : d.Parent.Name)).ToListAsync(ct);

        var now = Now;
        var ann = db.Announcements.AsNoTracking().Where(a => a.PublishAt <= now && (a.ExpiresAt == null || a.ExpiresAt > now));
        if (!currentUser.Has(Permissions.AnnouncementsManage)) ann = ann.Where(a => a.StoreId == null || a.StoreId == currentUser.StoreId);
        var announcements = await ann.Where(a => a.Title.ToLower().Contains(lower) || a.Body.ToLower().Contains(lower))
            .OrderByDescending(a => a.PublishAt).Take(5)
            .Select(a => new SearchRefDto(a.Id, a.Title, a.Store == null ? "Tüm şirket" : a.Store.Name)).ToListAsync(ct);

        var tickets = new List<SearchRefDto>();
        if (currentUser.HasAny(Permissions.TicketsReadOwn, Permissions.TicketsReadStore, Permissions.TicketsReadAll))
        {
            var visible = db.Tickets.AsNoTracking().Visible(currentUser);
            var id = int.TryParse(s.TrimStart('#'), out var n) ? n : -1;
            tickets = await visible.Where(t => t.Id == id || t.Title.ToLower().Contains(lower))
                .OrderByDescending(t => t.Id == id).ThenByDescending(t => t.CreatedAt).Take(5)
                .Select(t => new SearchRefDto(t.Id, t.Title, "#" + t.Id)).ToListAsync(ct);
        }
        return new SearchResultDto(people, departments, announcements, tickets);
    }

    // ---------- Yönetim: etkinlikler ----------

    public async Task<IReadOnlyList<CompanyEventDto>> AllEventsAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage);
        var rows = await db.CompanyEvents.AsNoTracking().OrderByDescending(e => e.StartsAt).Take(200).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<CompanyEventDto> SaveEventAsync(int? id, UpsertCompanyEventRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage);
        var userId = currentUser.RequireUserId();
        var e = id is null ? db.CompanyEvents.Add(new CompanyEvent { CreatedById = userId }).Entity
            : await db.CompanyEvents.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Etkinlik", id);
        e.Title = Text.Clean(r.Title);
        e.Description = Text.CleanOrNull(r.Description);
        e.Location = Text.CleanOrNull(r.Location);
        e.StartsAt = DateTime.SpecifyKind(r.StartsAt, DateTimeKind.Utc);
        e.EndsAt = r.EndsAt is null ? null : DateTime.SpecifyKind(r.EndsAt.Value, DateTimeKind.Utc);
        e.IsPublished = r.IsPublished;
        if (id is not null) e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        audit.Add(id is null ? "event.created" : "event.updated", "company_event", e.Id, new { e.Title, e.StartsAt });
        await db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task DeleteEventAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage);
        var e = await db.CompanyEvents.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Etkinlik", id);
        db.CompanyEvents.Remove(e);
        audit.Add("event.deleted", "company_event", id, new { e.Title });
        await db.SaveChangesAsync(ct);
    }

    // ---------- Yönetim: takdirler (kurumsal içerik ekibi veya İK) ----------

    private void RequireRecognitionManager() => currentUser.Require(Permissions.ContentManage, Permissions.EmployeesManage);

    private IQueryable<RecognitionDto> RecognitionQuery(IQueryable<Recognition> q) => q.Select(r => new RecognitionDto(r.Id, r.Type, r.Title, r.Description, r.Date,
        new PersonRefDto(r.User.Id, r.User.FullName, r.User.Position == null ? null : r.User.Position.Title, r.User.Department == null ? null : r.User.Department.Name,
            r.User.Profile != null && r.User.Profile.PhotoKey != null, r.User.Profile == null ? null : r.User.Profile.PhotoKey)));

    public async Task<IReadOnlyList<RecognitionDto>> AllRecognitionsAsync(CancellationToken ct = default)
    {
        RequireRecognitionManager();
        return await RecognitionQuery(db.Recognitions.AsNoTracking().OrderByDescending(r => r.Date).Take(200)).ToListAsync(ct);
    }

    public async Task<RecognitionDto> SaveRecognitionAsync(int? id, UpsertRecognitionRequest r, CancellationToken ct = default)
    {
        RequireRecognitionManager();
        var userId = currentUser.RequireUserId();
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId && u.IsActive, ct)) throw NotFoundException.For("Çalışan", r.UserId);
        var e = id is null ? db.Recognitions.Add(new Recognition { CreatedById = userId }).Entity
            : await db.Recognitions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Takdir", id);
        e.UserId = r.UserId;
        e.Type = r.Type;
        e.Title = Text.Clean(r.Title);
        e.Description = Text.CleanOrNull(r.Description);
        e.Date = r.Date;
        if (id is not null) e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        audit.Add(id is null ? "recognition.created" : "recognition.updated", "recognition", e.Id, new { e.UserId, type = e.Type.ToString() });
        await db.SaveChangesAsync(ct);
        return await RecognitionQuery(db.Recognitions.AsNoTracking().Where(x => x.Id == e.Id)).FirstAsync(ct);
    }

    public async Task DeleteRecognitionAsync(int id, CancellationToken ct = default)
    {
        RequireRecognitionManager();
        var e = await db.Recognitions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Takdir", id);
        db.Recognitions.Remove(e);
        audit.Add("recognition.deleted", "recognition", id);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class UpsertCompanyEventRequestValidator : AbstractValidator<UpsertCompanyEventRequest>
{
    public UpsertCompanyEventRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Etkinlik adı zorunludur.").MaximumLength(150).NoMarkup();
        RuleFor(x => x.Description).MaximumLength(2000).NoMarkup();
        RuleFor(x => x.Location).MaximumLength(150).NoMarkup();
        RuleFor(x => x.EndsAt).Must((r, e) => e is null || e >= r.StartsAt).WithMessage("Bitiş, başlangıçtan önce olamaz.");
    }
}

public sealed class UpsertRecognitionRequestValidator : AbstractValidator<UpsertRecognitionRequest>
{
    public UpsertRecognitionRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("Çalışan seçin.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").MaximumLength(150).NoMarkup();
        RuleFor(x => x.Description).MaximumLength(1000).NoMarkup();
    }
}
