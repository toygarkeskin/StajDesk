using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Tickets;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Site;

/* =========================================================
   Kurumsal karşılama sitesi (giriş öncesi, herkese açık) ve içerik yönetimi.
   KVKK kararı: herkese açık sitede kişisel veri yok (çalışan adı, doğum günü, fotoğraf);
   bunlar yalnızca giriş sonrası portalda.
   ========================================================= */

public static class SiteKeys
{
    public const string CompanyName = "company.name";
    public const string Tagline = "company.tagline";
    public const string AboutTitle = "about.title";
    public const string AboutBody = "about.body";
    public const string Mission = "about.mission";
    public const string Vision = "about.vision";
    public const string ContactAddress = "contact.address";
    public const string ContactPhone = "contact.phone";
    public const string ContactEmail = "contact.email";
    public const string CareerEmail = "career.email";
    public const string WeatherCity = "weather.city";
    /// <summary>JSON: [{ "label": "...", "url": "https://..." }]</summary>
    public const string Links = "links";

    public static readonly string[] All = [CompanyName, Tagline, AboutTitle, AboutBody, Mission, Vision, ContactAddress, ContactPhone, ContactEmail, CareerEmail, WeatherCity, Links];
}

public sealed record LinkDto(string Label, string Url);
public sealed record SiteInfoDto(IReadOnlyDictionary<string, string> Settings, IReadOnlyList<LinkDto> Links);
public sealed record AdDto(int Id, string Title, string? Description, string ImageUrl, bool HasLink, AdPlacement Placement);
public sealed record NewsCardDto(int Id, string Slug, string Title, string Summary, string? ImageUrl, DateTime PublishedAt);
public sealed record NewsDetailDto(int Id, string Slug, string Title, string Summary, string Body, string? ImageUrl, DateTime PublishedAt);
public sealed record JobCardDto(int Id, string Title, string? Department, string Location, EmploymentType EmploymentType, DateTime PublishedAt, DateOnly? ClosesOn);
public sealed record JobDetailDto(int Id, string Title, string? Department, string Location, EmploymentType EmploymentType, string Description, string? Requirements,
    DateTime PublishedAt, DateOnly? ClosesOn, string? ApplyEmail);
public sealed record PublicDepartmentDto(int Id, string Name, string? Description, int MemberCount, IReadOnlyList<string> Units);
public sealed record PublicAnnouncementDto(int Id, string Title, string Body, AnnouncementLevel Level, DateTime PublishAt);
public sealed record CompanyStatsDto(int Employees, int Stores, int Departments, int Cities);
public sealed record LandingDto(SiteInfoDto Site, IReadOnlyList<AdDto> Ads, IReadOnlyList<NewsCardDto> News, IReadOnlyList<PublicAnnouncementDto> Announcements,
    IReadOnlyList<PublicDepartmentDto> Departments, IReadOnlyList<JobCardDto> Jobs, CompanyStatsDto Stats);
public sealed record MediaContent(Stream Content, string ContentType);

public interface IPublicSiteService
{
    Task<LandingDto> GetLandingAsync(CancellationToken ct = default);
    Task<SiteInfoDto> GetSiteInfoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdDto>> GetAdsAsync(AdPage page, CancellationToken ct = default);
    Task<PagedResult<NewsCardDto>> GetNewsAsync(PagedQuery query, CancellationToken ct = default);
    Task<NewsDetailDto> GetNewsAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<JobCardDto>> GetJobsAsync(CancellationToken ct = default);
    Task<JobDetailDto> GetJobAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PublicDepartmentDto>> GetDepartmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PublicAnnouncementDto>> GetAnnouncementsAsync(CancellationToken ct = default);
    Task RecordImpressionsAsync(IReadOnlyList<int> adIds, CancellationToken ct = default);
    /// <summary>Tıklamayı sayar ve yönlendirilecek adresi döner (yoksa null).</summary>
    Task<string?> RecordClickAsync(int adId, CancellationToken ct = default);
    Task<MediaContent?> GetMediaAsync(string key, CancellationToken ct = default);
}

public static class MediaUrls
{
    public static string Of(string key) => $"/api/public/media/{key}";
    public static string? OfNullable(string? key) => key is null ? null : Of(key);
}

internal sealed class PublicSiteService(IAppDbContext db, IFileStorage storage, ICacheService cache, TimeProvider clock) : IPublicSiteService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<LandingDto> GetLandingAsync(CancellationToken ct = default) =>
        // Herkese açık sayfa en çok istek alan uç: 2 dk önbellek. İçerik değişince "site:" önbelleği silinir.
        await cache.GetOrSetAsync(CacheKeys.SitePrefix + "landing", async token => new LandingDto(
            await GetSiteInfoAsync(token),
            await GetAdsAsync(AdPage.Landing, token),
            (await GetNewsAsync(new SimplePaging { PageSize = 4 }, token)).Items,
            (await GetAnnouncementsAsync(token)).Take(4).ToList(),
            await GetDepartmentsAsync(token),
            (await GetJobsAsync(token)).Take(4).ToList(),
            await StatsAsync(token)), Ttl, ct);

    public async Task<SiteInfoDto> GetSiteInfoAsync(CancellationToken ct = default)
    {
        var rows = await db.SiteSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        var links = rows.TryGetValue(SiteKeys.Links, out var json) ? ParseLinks(json) : [];
        rows.Remove(SiteKeys.Links);
        return new SiteInfoDto(rows, links);
    }

    internal static IReadOnlyList<LinkDto> ParseLinks(string json)
    {
        try { return JsonSerializer.Deserialize<List<LinkDto>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []; }
        catch (JsonException) { return []; }
    }

    public async Task<IReadOnlyList<AdDto>> GetAdsAsync(AdPage page, CancellationToken ct = default)
    {
        var now = Now;
        return await db.Advertisements.AsNoTracking()
            .Where(a => a.Page == page && a.IsActive && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .OrderBy(a => a.Placement).ThenBy(a => a.SortOrder).ThenByDescending(a => a.StartsAt)
            .Select(a => new AdDto(a.Id, a.Title, a.Description, "/api/public/media/" + a.ImageKey, a.LinkUrl != null, a.Placement))
            .ToListAsync(ct);
    }

    private IQueryable<NewsArticle> PublishedNews() => db.News.AsNoTracking().Where(n => n.IsPublished && n.PublishedAt <= Now);

    public async Task<PagedResult<NewsCardDto>> GetNewsAsync(PagedQuery query, CancellationToken ct = default) =>
        await PublishedNews().OrderByDescending(n => n.PublishedAt)
            .Select(n => new NewsCardDto(n.Id, n.Slug, n.Title, n.Summary, n.ImageKey == null ? null : "/api/public/media/" + n.ImageKey, n.PublishedAt))
            .ToPagedResultAsync(query, ct);

    public async Task<NewsDetailDto> GetNewsAsync(string slug, CancellationToken ct = default) =>
        await PublishedNews().Where(n => n.Slug == slug)
            .Select(n => new NewsDetailDto(n.Id, n.Slug, n.Title, n.Summary, n.Body, n.ImageKey == null ? null : "/api/public/media/" + n.ImageKey, n.PublishedAt))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Haber bulunamadı veya yayından kaldırıldı.");

    private IQueryable<JobOpening> OpenJobs()
    {
        var today = DateOnly.FromDateTime(Now);
        return db.JobOpenings.AsNoTracking().Where(j => j.IsPublished && j.PublishedAt <= Now && (j.ClosesOn == null || j.ClosesOn >= today));
    }

    public async Task<IReadOnlyList<JobCardDto>> GetJobsAsync(CancellationToken ct = default) =>
        await OpenJobs().OrderByDescending(j => j.PublishedAt)
            .Select(j => new JobCardDto(j.Id, j.Title, j.Department == null ? null : j.Department.Name, j.Location, j.EmploymentType, j.PublishedAt, j.ClosesOn))
            .ToListAsync(ct);

    public async Task<JobDetailDto> GetJobAsync(int id, CancellationToken ct = default)
    {
        var applyEmail = await db.SiteSettings.AsNoTracking().Where(s => s.Key == SiteKeys.CareerEmail).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return await OpenJobs().Where(j => j.Id == id)
            .Select(j => new JobDetailDto(j.Id, j.Title, j.Department == null ? null : j.Department.Name, j.Location, j.EmploymentType, j.Description, j.Requirements,
                j.PublishedAt, j.ClosesOn, applyEmail))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("İlan bulunamadı veya başvuru süresi doldu.");
    }

    public async Task<IReadOnlyList<PublicDepartmentDto>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        var all = await db.Departments.AsNoTracking().Where(d => d.IsActive)
            .Select(d => new { d.Id, d.Name, d.Description, d.ParentId, d.ShowOnWebsite, Members = d.Members.Count(m => m.IsActive) }).ToListAsync(ct);
        // Kişi sayısı ve birim adları kişisel veri değildir; yöneticinin adı herkese açık sitede gösterilmez.
        return all.Where(d => d.ShowOnWebsite).OrderBy(d => d.Name)
            .Select(d => new PublicDepartmentDto(d.Id, d.Name, d.Description, d.Members, all.Where(c => c.ParentId == d.Id).Select(c => c.Name).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<PublicAnnouncementDto>> GetAnnouncementsAsync(CancellationToken ct = default)
    {
        var now = Now;
        return await db.Announcements.AsNoTracking()
            .Where(a => a.IsPublic && a.StoreId == null && a.TargetDepartmentId == null && a.PublishAt <= now && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishAt).Take(20)
            .Select(a => new PublicAnnouncementDto(a.Id, a.Title, a.Body, a.Level, a.PublishAt))
            .ToListAsync(ct);
    }

    private async Task<CompanyStatsDto> StatsAsync(CancellationToken ct) => new(
        await db.Users.CountAsync(u => u.IsActive && u.EmailVerifiedAt != null, ct),
        await db.Stores.CountAsync(s => s.IsActive, ct),
        await db.Departments.CountAsync(d => d.IsActive, ct),
        await db.Stores.Where(s => s.IsActive && s.City != null).Select(s => s.City).Distinct().CountAsync(ct));

    /// <summary>Yalnızca o an yayında olan reklamların gösterimi sayılır (sahte sayım engellenir); istek başına en fazla 10 reklam.</summary>
    public async Task RecordImpressionsAsync(IReadOnlyList<int> adIds, CancellationToken ct = default)
    {
        var ids = adIds.Distinct().Take(10).ToList();
        if (ids.Count == 0) return;
        var now = Now;
        var live = await db.Advertisements.Where(a => ids.Contains(a.Id) && a.IsActive && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.Id).ToListAsync(ct);
        foreach (var id in live) (await StatRowAsync(id, ct)).Impressions++;
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> RecordClickAsync(int adId, CancellationToken ct = default)
    {
        var now = Now;
        var ad = await db.Advertisements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == adId, ct) ?? throw NotFoundException.For("Reklam", adId);
        if (!ad.IsLive(now) || ad.LinkUrl is null) return ad.LinkUrl; // yayında değilse sayılmaz
        (await StatRowAsync(adId, ct)).Clicks++;
        await db.SaveChangesAsync(ct);
        return ad.LinkUrl;
    }

    private async Task<AdStatDaily> StatRowAsync(int adId, CancellationToken ct)
    {
        var day = DateOnly.FromDateTime(Now);
        var row = db.AdStats.Local.FirstOrDefault(s => s.AdvertisementId == adId && s.Day == day)
                  ?? await db.AdStats.FirstOrDefaultAsync(s => s.AdvertisementId == adId && s.Day == day, ct);
        if (row is null)
        {
            row = new AdStatDaily { AdvertisementId = adId, Day = day };
            db.AdStats.Add(row);
        }
        return row;
    }

    /// <summary>Yalnızca içerik yönetimiyle yüklenmiş (MediaFiles tablosundaki) görseller herkese açıktır.</summary>
    public async Task<MediaContent?> GetMediaAsync(string key, CancellationToken ct = default)
    {
        var media = await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.Key == key, ct);
        if (media is null) return null;
        var stream = await storage.OpenReadAsync(key, ct);
        return stream is null ? null : new MediaContent(stream, media.ContentType);
    }

    private sealed record SimplePaging : PagedQuery;
}

/* ---------------- İçerik yönetimi (portal içinden) ---------------- */

public sealed record AdAdminDto(int Id, string Title, string? Description, string ImageUrl, string ImageKey, string? LinkUrl, AdPage Page, AdPlacement Placement,
    int SortOrder, DateTime StartsAt, DateTime? EndsAt, bool IsActive, string Status, int Impressions, int Clicks, double ClickRate);
public sealed record UpsertAdRequest(string Title, string? Description, string ImageKey, string? LinkUrl, AdPage Page, AdPlacement Placement,
    int SortOrder, DateTime StartsAt, DateTime? EndsAt, bool IsActive);
public sealed record AdDailyStatDto(DateOnly Day, int Impressions, int Clicks);

public sealed record NewsAdminDto(int Id, string Slug, string Title, string Summary, string Body, string? ImageKey, string? ImageUrl, bool IsPublished, DateTime PublishedAt, string AuthorName);
public sealed record UpsertNewsRequest(string Title, string Summary, string Body, string? ImageKey, bool IsPublished, DateTime? PublishedAt);

public sealed record JobAdminDto(int Id, string Title, int? DepartmentId, string? Department, string Location, EmploymentType EmploymentType, string Description,
    string? Requirements, bool IsPublished, DateTime PublishedAt, DateOnly? ClosesOn);
public sealed record UpsertJobRequest(string Title, int? DepartmentId, string Location, EmploymentType EmploymentType, string Description, string? Requirements,
    bool IsPublished, DateOnly? ClosesOn);

public sealed record SaveSiteSettingsRequest(IReadOnlyDictionary<string, string> Settings, IReadOnlyList<LinkDto> Links);
public sealed record MediaUploadedDto(string Key, string Url);

public interface IContentAdminService
{
    Task<IReadOnlyList<AdAdminDto>> GetAdsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdDailyStatDto>> GetAdStatsAsync(int id, CancellationToken ct = default);
    Task<AdAdminDto> SaveAdAsync(int? id, UpsertAdRequest request, CancellationToken ct = default);
    Task DeleteAdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<NewsAdminDto>> GetNewsAsync(CancellationToken ct = default);
    Task<NewsAdminDto> SaveNewsAsync(int? id, UpsertNewsRequest request, CancellationToken ct = default);
    Task DeleteNewsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<JobAdminDto>> GetJobsAsync(CancellationToken ct = default);
    Task<JobAdminDto> SaveJobAsync(int? id, UpsertJobRequest request, CancellationToken ct = default);
    Task DeleteJobAsync(int id, CancellationToken ct = default);
    Task<SiteInfoDto> SaveSettingsAsync(SaveSiteSettingsRequest request, CancellationToken ct = default);
    Task<MediaUploadedDto> UploadMediaAsync(string fileName, Stream content, CancellationToken ct = default);
}

internal sealed class ContentAdminService(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, ICacheService cache, IAuditLogger audit, TimeProvider clock)
    : IContentAdminService
{
    public const long MaxImageBytes = 3 * 1024 * 1024;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private Task InvalidateAsync(CancellationToken ct) => cache.RemoveByPrefixAsync(CacheKeys.SitePrefix, ct);

    // ---------- Reklamlar ----------

    public async Task<IReadOnlyList<AdAdminDto>> GetAdsAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AdsManage);
        var now = Now;
        var rows = await db.Advertisements.AsNoTracking().OrderBy(a => a.Page).ThenBy(a => a.Placement).ThenBy(a => a.SortOrder)
            .Select(a => new { a, Impressions = a.Stats.Sum(s => s.Impressions), Clicks = a.Stats.Sum(s => s.Clicks) }).ToListAsync(ct);
        return rows.Select(r => ToDto(r.a, r.Impressions, r.Clicks, now)).ToList();
    }

    internal static string StatusOf(Advertisement a, DateTime now) =>
        !a.IsActive ? "inactive" : a.StartsAt > now ? "scheduled" : a.EndsAt is { } e && e <= now ? "expired" : "live";

    private static AdAdminDto ToDto(Advertisement a, int impressions, int clicks, DateTime now) => new(
        a.Id, a.Title, a.Description, MediaUrls.Of(a.ImageKey), a.ImageKey, a.LinkUrl, a.Page, a.Placement, a.SortOrder, a.StartsAt, a.EndsAt, a.IsActive,
        StatusOf(a, now), impressions, clicks, impressions == 0 ? 0 : Math.Round(clicks * 100.0 / impressions, 2));

    public async Task<IReadOnlyList<AdDailyStatDto>> GetAdStatsAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AdsManage);
        var since = DateOnly.FromDateTime(Now.AddDays(-29));
        return await db.AdStats.AsNoTracking().Where(s => s.AdvertisementId == id && s.Day >= since).OrderBy(s => s.Day)
            .Select(s => new AdDailyStatDto(s.Day, s.Impressions, s.Clicks)).ToListAsync(ct);
    }

    public async Task<AdAdminDto> SaveAdAsync(int? id, UpsertAdRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AdsManage);
        await EnsureMediaAsync(r.ImageKey, ct);
        Advertisement a;
        if (id is null) a = db.Advertisements.Add(new Advertisement()).Entity;
        else a = await db.Advertisements.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Reklam", id);
        a.Title = Text.Clean(r.Title);
        a.Description = Text.CleanOrNull(r.Description);
        a.ImageKey = r.ImageKey;
        a.LinkUrl = string.IsNullOrWhiteSpace(r.LinkUrl) ? null : r.LinkUrl.Trim();
        a.Page = r.Page;
        a.Placement = r.Placement;
        a.SortOrder = r.SortOrder;
        a.StartsAt = r.StartsAt.ToUniversalTime();
        a.EndsAt = r.EndsAt?.ToUniversalTime();
        a.IsActive = r.IsActive;
        a.UpdatedAt = id is null ? null : Now;
        audit.Add(id is null ? "ad.created" : "ad.updated", "advertisement", id, new { a.Title, a.Page, a.Placement });
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ct);
        return (await GetAdsAsync(ct)).First(x => x.Id == a.Id);
    }

    public async Task DeleteAdAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AdsManage);
        var a = await db.Advertisements.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Reklam", id);
        db.Advertisements.Remove(a);
        audit.Add("ad.deleted", "advertisement", id, new { a.Title });
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ct);
    }

    // ---------- Haberler ----------

    public async Task<IReadOnlyList<NewsAdminDto>> GetNewsAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage);
        return await db.News.AsNoTracking().OrderByDescending(n => n.PublishedAt)
            .Select(n => new NewsAdminDto(n.Id, n.Slug, n.Title, n.Summary, n.Body, n.ImageKey, n.ImageKey == null ? null : "/api/public/media/" + n.ImageKey,
                n.IsPublished, n.PublishedAt, n.Author.FullName))
            .ToListAsync(ct);
    }

    public async Task<NewsAdminDto> SaveNewsAsync(int? id, UpsertNewsRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage);
        var userId = currentUser.RequireUserId();
        if (r.ImageKey is not null) await EnsureMediaAsync(r.ImageKey, ct);
        NewsArticle n;
        if (id is null) n = db.News.Add(new NewsArticle { AuthorId = userId }).Entity;
        else n = await db.News.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Haber", id);
        n.Title = Text.Clean(r.Title);
        n.Summary = Text.Clean(r.Summary);
        n.Body = Text.Clean(r.Body);
        n.ImageKey = r.ImageKey;
        n.IsPublished = r.IsPublished;
        n.PublishedAt = r.PublishedAt?.ToUniversalTime() ?? (id is null ? Now : n.PublishedAt);
        if (id is null) n.Slug = await UniqueSlugAsync(n.Title, ct);
        n.UpdatedAt = id is null ? null : Now;
        audit.Add(id is null ? "news.created" : "news.updated", "news", id, new { n.Title, n.IsPublished });
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ct);
        return (await GetNewsAsync(ct)).First(x => x.Id == n.Id);
    }

    public async Task DeleteNewsAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage);
        var n = await db.News.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Haber", id);
        db.News.Remove(n);
        audit.Add("news.deleted", "news", id, new { n.Title });
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ct);
    }

    /// <summary>Türkçe karakterleri sadeleştirerek okunabilir, tekil adres: "Yeni Mağazamız Açıldı" → "yeni-magazamiz-acildi".</summary>
    internal static string Slugify(string title)
    {
        var map = new Dictionary<char, string> { ['ç'] = "c", ['ğ'] = "g", ['ı'] = "i", ['İ'] = "i", ['ö'] = "o", ['ş'] = "s", ['ü'] = "u", ['Ç'] = "c", ['Ğ'] = "g", ['Ö'] = "o", ['Ş'] = "s", ['Ü'] = "u" };
        var sb = new StringBuilder();
        foreach (var ch in title.Trim())
        {
            if (map.TryGetValue(ch, out var rep)) sb.Append(rep);
            else if (char.IsAsciiLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        if (slug.Length > 100) slug = slug[..100].TrimEnd('-');
        return slug.Length == 0 ? "haber" : slug;
    }

    private async Task<string> UniqueSlugAsync(string title, CancellationToken ct)
    {
        var baseSlug = Slugify(title);
        var slug = baseSlug;
        for (var i = 2; await db.News.AnyAsync(n => n.Slug == slug, ct); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }

    // ---------- Açık pozisyonlar ----------

    public async Task<IReadOnlyList<JobAdminDto>> GetJobsAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage);
        return await db.JobOpenings.AsNoTracking().OrderByDescending(j => j.PublishedAt)
            .Select(j => new JobAdminDto(j.Id, j.Title, j.DepartmentId, j.Department == null ? null : j.Department.Name, j.Location, j.EmploymentType,
                j.Description, j.Requirements, j.IsPublished, j.PublishedAt, j.ClosesOn))
            .ToListAsync(ct);
    }

    public async Task<JobAdminDto> SaveJobAsync(int? id, UpsertJobRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage);
        if (r.DepartmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) throw NotFoundException.For("Departman", d);
        JobOpening j;
        if (id is null) j = db.JobOpenings.Add(new JobOpening()).Entity;
        else j = await db.JobOpenings.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("İlan", id);
        var wasPublished = j.IsPublished;
        j.Title = Text.Clean(r.Title);
        j.DepartmentId = r.DepartmentId;
        j.Location = Text.Clean(r.Location);
        j.EmploymentType = r.EmploymentType;
        j.Description = Text.Clean(r.Description);
        j.Requirements = Text.CleanOrNull(r.Requirements);
        j.IsPublished = r.IsPublished;
        if (r.IsPublished && !wasPublished) j.PublishedAt = Now;
        j.ClosesOn = r.ClosesOn;
        audit.Add(id is null ? "job.created" : "job.updated", "job_opening", id, new { j.Title, j.IsPublished });
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ct);
        return (await GetJobsAsync(ct)).First(x => x.Id == j.Id);
    }

    public async Task DeleteJobAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage);
        var j = await db.JobOpenings.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("İlan", id);
        db.JobOpenings.Remove(j);
        audit.Add("job.deleted", "job_opening", id, new { j.Title });
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ct);
    }

    // ---------- Site metinleri ----------

    public async Task<SiteInfoDto> SaveSettingsAsync(SaveSiteSettingsRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage);
        var unknown = r.Settings.Keys.Where(k => !SiteKeys.All.Contains(k) || k == SiteKeys.Links).ToList();
        if (unknown.Count > 0) throw new DomainException($"Bilinmeyen ayar: {string.Join(", ", unknown)}");
        var rows = await db.SiteSettings.ToDictionaryAsync(s => s.Key, ct);
        void Set(string key, string value)
        {
            if (!rows.TryGetValue(key, out var row)) { row = new SiteSetting { Key = key }; db.SiteSettings.Add(row); rows[key] = row; }
            row.Value = value;
            row.UpdatedAt = Now;
        }
        foreach (var (k, v) in r.Settings) Set(k, Text.Clean(v));
        Set(SiteKeys.Links, JsonSerializer.Serialize(r.Links.Select(l => new LinkDto(Text.Clean(l.Label), l.Url.Trim())),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        audit.Add("site.settings_updated", "site", null, new { keys = r.Settings.Keys });
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ct);
        var all = await db.SiteSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        var links = all.TryGetValue(SiteKeys.Links, out var json) ? PublicSiteService.ParseLinks(json) : [];
        all.Remove(SiteKeys.Links);
        return new SiteInfoDto(all, links);
    }

    // ---------- Görsel yükleme ----------

    public async Task<MediaUploadedDto> UploadMediaAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.ContentManage, Permissions.AdsManage);
        var userId = currentUser.RequireUserId();
        var ext = Path.GetExtension(AttachmentRules.SafeFileName(fileName)).ToLowerInvariant();
        var type = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", _ => null }
                   ?? throw new DomainException("Görsel PNG, JPG veya WEBP olmalıdır.");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0) throw new DomainException("Dosya boş görünüyor.");
        if (buffer.Length > MaxImageBytes) throw new DomainException("Görsel 3 MB'tan büyük olamaz.");
        if (!AttachmentRules.SignatureMatches(ext, buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 64))))
            throw new DomainException("Dosyanın içeriği bir görsel değil. PNG, JPG veya WEBP yükleyin.");
        var key = Guid.NewGuid().ToString("N");
        buffer.Position = 0;
        await storage.SaveAsync(key, buffer, ct);
        db.MediaFiles.Add(new MediaFile { Key = key, ContentType = type, Size = buffer.Length, FileName = AttachmentRules.SafeFileName(fileName), UploadedById = userId });
        await db.SaveChangesAsync(ct);
        return new MediaUploadedDto(key, MediaUrls.Of(key));
    }

    private async Task EnsureMediaAsync(string key, CancellationToken ct)
    {
        if (!await db.MediaFiles.AnyAsync(m => m.Key == key, ct)) throw new DomainException("Görsel bulunamadı. Önce görseli yükleyin.");
    }
}

/* ---------------- Doğrulama ---------------- */

public static class UrlRules
{
    /// <summary>Yalnızca mutlak http/https adresi (javascript:, data: vb. reddedilir).</summary>
    public static bool IsSafeHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);
}

public sealed class UpsertAdRequestValidator : AbstractValidator<UpsertAdRequest>
{
    public UpsertAdRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Reklam başlığı zorunludur.").MaximumLength(150).NoMarkup();
        RuleFor(x => x.Description).MaximumLength(300).NoMarkup();
        RuleFor(x => x.ImageKey).NotEmpty().WithMessage("Reklam görselini yükleyin.");
        RuleFor(x => x.LinkUrl).Must(u => string.IsNullOrWhiteSpace(u) || UrlRules.IsSafeHttpUrl(u))
            .WithMessage("Bağlantı https:// ile başlayan geçerli bir adres olmalıdır.");
        RuleFor(x => x.Page).IsInEnum();
        RuleFor(x => x.Placement).IsInEnum();
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1000);
        RuleFor(x => x.EndsAt).Must((r, e) => e is null || e > r.StartsAt).WithMessage("Bitiş tarihi başlangıçtan sonra olmalıdır.");
    }
}

public sealed class UpsertNewsRequestValidator : AbstractValidator<UpsertNewsRequest>
{
    public UpsertNewsRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").MaximumLength(200).NoMarkup();
        RuleFor(x => x.Summary).NotEmpty().WithMessage("Kısa özet zorunludur.").MaximumLength(400).NoMarkup();
        RuleFor(x => x.Body).NotEmpty().WithMessage("Haber metni zorunludur.").MaximumLength(20000).NoMarkup();
    }
}

public sealed class UpsertJobRequestValidator : AbstractValidator<UpsertJobRequest>
{
    public UpsertJobRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Pozisyon adı zorunludur.").MaximumLength(150).NoMarkup();
        RuleFor(x => x.Location).NotEmpty().WithMessage("Çalışma yeri zorunludur.").MaximumLength(100).NoMarkup();
        RuleFor(x => x.Description).NotEmpty().WithMessage("İlan açıklaması zorunludur.").MaximumLength(5000).NoMarkup();
        RuleFor(x => x.Requirements).MaximumLength(5000).NoMarkup();
        RuleFor(x => x.EmploymentType).IsInEnum();
    }
}

public sealed class SaveSiteSettingsRequestValidator : AbstractValidator<SaveSiteSettingsRequest>
{
    public SaveSiteSettingsRequestValidator()
    {
        RuleForEach(x => x.Settings).Must(kv => kv.Value.Length <= 5000 && !Text.ContainsMarkup(kv.Value))
            .WithMessage("Metinler düz metin olmalı ve 5000 karakteri geçmemelidir.");
        RuleFor(x => x.Links).Must(l => l.Count <= 20).WithMessage("En fazla 20 bağlantı eklenebilir.");
        RuleForEach(x => x.Links).Must(l => !string.IsNullOrWhiteSpace(l.Label) && l.Label.Length <= 60 && UrlRules.IsSafeHttpUrl(l.Url))
            .WithMessage("Her bağlantının adı olmalı ve adresi https:// ile başlamalıdır.");
    }
}
