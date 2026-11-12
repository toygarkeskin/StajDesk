using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Portal;

/* =========================================================
   Widget (panel kutucuğu) sistemi.
   - Katalog kodda: her kutucuğun anahtarı, adı, varsayılan boyut/sırası ve GEREKTİRDİĞİ İZİN.
     İzin yöneticinin ayarıyla gevşetilemez: izni olmayan kullanıcı kutucuğu hiçbir koşulda görmez.
   - Yönetici (widgets.manage): açık/kapalı, sıra, boyut, hangi rollerin göreceği.
   - Kullanıcı: kendi sırası ve gizledikleri. "Varsayılana dön" ile tercihleri silinir.
   ========================================================= */

public sealed record WidgetDefinition(string Key, string Title, string Description, string Category, WidgetSize DefaultSize, int DefaultOrder, string[] RequiredAnyPermission);

public static class WidgetCatalog
{
    public static readonly IReadOnlyList<WidgetDefinition> All =
    [
        new("welcome", "Karşılama", "Selamlama, hızlı talep açma ve bildirim sayısı", "Kişisel", WidgetSize.Small, 10, []),
        new("profile", "Profil özeti", "Departman, unvan, yönetici, işe başlama ve kıdem", "Kişisel", WidgetSize.Small, 20, []),
        new("leave-balance", "İzin bakiyem", "Bu yılki izin haklarınız, kullanılan ve kalan günler", "Kişisel", WidgetSize.Small, 25, [Permissions.LeaveRequest]),
        new("approval-inbox", "Onay kutum", "Kararınızı bekleyen izin ve iş akışı onayları", "Kişisel", WidgetSize.Small, 26, []),
        new("announcements", "Duyurular", "Yayındaki şirket ve mağaza duyuruları", "Kurumsal", WidgetSize.Small, 30, []),
        new("oncall", "BT nöbetçi personel", "Bu haftanın nöbetçisi ve iletişim bilgileri", "Kurumsal", WidgetSize.Small, 40, []),
        new("weather", "Hava durumu", "Canlı hava durumu (Open-Meteo)", "Bilgi", WidgetSize.Small, 50, []),
        new("rates", "Döviz kurları", "TCMB gösterge kurları", "Bilgi", WidgetSize.Small, 60, []),
        new("gold", "Gram altın", "Gram altın (kaynak yapılandırılınca)", "Bilgi", WidgetSize.Small, 70, []),
        new("ticket-counters", "Talep sayaçları", "Yetki kapsamınızdaki açık, işlemde, bekleyen, çözülen talepler", "Destek", WidgetSize.Full, 80, []),
        new("approvals", "Onayımı bekleyenler", "Çözüldü işaretlenen ve onayınızı bekleyen talepleriniz", "Destek", WidgetSize.Small, 90, []),
        new("tasks", "Görevlerim / taleplerim", "Size atanan açık talepler veya son açtığınız talepler", "Destek", WidgetSize.Small, 100, []),
        new("activity", "Son hareketler", "Görebildiğiniz taleplerdeki son değişiklikler", "Destek", WidgetSize.Small, 110, []),
        new("birthdays", "Doğum günleri", "Önümüzdeki 7 gün (yalnızca paylaşmayı seçen çalışanlar)", "Kurumsal", WidgetSize.Small, 120, []),
        new("new-hires", "Aramıza katılanlar", "Son 30 günde işe başlayan çalışanlar", "Kurumsal", WidgetSize.Small, 130, []),
        new("recognitions", "Takdirler", "Haftanın çalışanı ve çalışan başarıları", "Kurumsal", WidgetSize.Small, 140, []),
        new("events", "Şirket etkinlikleri", "Yaklaşan etkinlikler", "Kurumsal", WidgetSize.Small, 150, []),
        new("open-positions", "Açık pozisyonlar", "Kariyer sayfasındaki açık ilanlar", "Kurumsal", WidgetSize.Small, 160, []),
        new("portal-ads", "Portal duyuru bannerları", "Kurumsal site yönetiminden portal için yayınlanan bannerlar", "Kurumsal", WidgetSize.Small, 170, []),
        new("people-search", "Hızlı kişi arama", "Kişi, e-posta, cihaz arama", "Yönetim", WidgetSize.Small, 180,
            [Permissions.TicketsManage, Permissions.UsersManage, Permissions.AssetsManage]),
        new("pending-accounts", "Bekleyen hesap talepleri", "Onay bekleyen yeni hesap ve kapatma talepleri", "Yönetim", WidgetSize.Small, 190,
            [Permissions.AccountsRequest, Permissions.UsersManage]),
        new("ticket-trend", "Talep eğilimi", "Son 14 günün açılan / çözülen talepleri", "Destek", WidgetSize.Large, 200, []),
        new("ticket-charts", "Talep dağılımı", "Kategori, öncelik ve durum grafikleri", "Destek", WidgetSize.Full, 210, []),
    ];

    public static WidgetDefinition? Find(string key) => All.FirstOrDefault(w => w.Key == key);
}

public sealed record DashboardWidgetDto(string Key, string Title, string Description, string Category, WidgetSize Size);
public sealed record DashboardLayoutDto(IReadOnlyList<DashboardWidgetDto> Widgets, IReadOnlyList<DashboardWidgetDto> Hidden, bool Customized);
public sealed record SaveDashboardPreferencesRequest(IReadOnlyList<string> Order, IReadOnlyList<string> Hidden);

public sealed record WidgetAdminDto(string Key, string Title, string Description, string Category, IReadOnlyList<string> RequiredAnyPermission,
    bool IsEnabled, int SortOrder, WidgetSize Size, IReadOnlyList<string> Roles, WidgetSize DefaultSize, int DefaultOrder);
public sealed record WidgetRoleDto(string Name, string DisplayName);
public sealed record WidgetAdminListDto(IReadOnlyList<WidgetAdminDto> Widgets, IReadOnlyList<WidgetRoleDto> Roles);
public sealed record SaveWidgetRequest(bool IsEnabled, int SortOrder, WidgetSize Size, IReadOnlyList<string> Roles);

public interface IDashboardService
{
    Task<DashboardLayoutDto> GetLayoutAsync(CancellationToken ct = default);
    Task<DashboardLayoutDto> SavePreferencesAsync(SaveDashboardPreferencesRequest request, CancellationToken ct = default);
    Task<DashboardLayoutDto> ResetPreferencesAsync(CancellationToken ct = default);
    Task<WidgetAdminListDto> GetWidgetsAsync(CancellationToken ct = default);
    Task<WidgetAdminDto> SaveWidgetAsync(string key, SaveWidgetRequest request, CancellationToken ct = default);
}

internal sealed class DashboardService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit) : IDashboardService
{
    /// <summary>Kullanıcının izinleri ve rolleri ile yöneticinin ayarına göre görebileceği kutucuklar (katalog sırasıyla).</summary>
    private async Task<List<(WidgetDefinition Def, int Order, WidgetSize Size)>> AllowedAsync(CancellationToken ct)
    {
        var configs = await db.DashboardWidgets.AsNoTracking().ToDictionaryAsync(c => c.Key, ct);
        var roles = currentUser.Roles;
        var list = new List<(WidgetDefinition, int, WidgetSize)>();
        foreach (var def in WidgetCatalog.All)
        {
            if (def.RequiredAnyPermission.Length > 0 && !currentUser.HasAny(def.RequiredAnyPermission)) continue;
            var cfg = configs.GetValueOrDefault(def.Key);
            if (cfg is not null && !cfg.IsEnabled) continue;
            if (cfg is { Roles.Count: > 0 } && !cfg.Roles.Any(r => roles.Contains(r))) continue;
            list.Add((def, cfg?.SortOrder ?? def.DefaultOrder, cfg?.Size ?? def.DefaultSize));
        }
        return list;
    }

    private static DashboardWidgetDto ToDto(WidgetDefinition d, WidgetSize size) => new(d.Key, d.Title, d.Description, d.Category, size);

    public async Task<DashboardLayoutDto> GetLayoutAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var allowed = await AllowedAsync(ct);
        var prefs = await db.DashboardPreferences.AsNoTracking().Where(p => p.UserId == userId).ToDictionaryAsync(p => p.WidgetKey, ct);
        var ordered = allowed
            .OrderBy(a => prefs.TryGetValue(a.Def.Key, out var p) ? p.SortOrder : int.MaxValue) // kişisel sıra önce; yeni kutucuklar sona
            .ThenBy(a => a.Order).ToList();
        return new DashboardLayoutDto(
            ordered.Where(a => !(prefs.GetValueOrDefault(a.Def.Key)?.Hidden ?? false)).Select(a => ToDto(a.Def, a.Size)).ToList(),
            ordered.Where(a => prefs.GetValueOrDefault(a.Def.Key)?.Hidden ?? false).Select(a => ToDto(a.Def, a.Size)).ToList(),
            prefs.Count > 0);
    }

    public async Task<DashboardLayoutDto> SavePreferencesAsync(SaveDashboardPreferencesRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var allowed = (await AllowedAsync(ct)).Select(a => a.Def.Key).ToHashSet();
        // Yalnızca kullanıcının görebileceği kutucuklar kaydedilir; bilinmeyen/yetkisiz anahtarlar yok sayılır.
        var order = (r.Order ?? []).Concat(r.Hidden ?? []).Where(allowed.Contains).Distinct().ToList();
        var hidden = (r.Hidden ?? []).Where(allowed.Contains).ToHashSet();
        db.DashboardPreferences.RemoveRange(await db.DashboardPreferences.Where(p => p.UserId == userId).ToListAsync(ct));
        for (var i = 0; i < order.Count; i++)
            db.DashboardPreferences.Add(new UserDashboardPreference { UserId = userId, WidgetKey = order[i], SortOrder = i, Hidden = hidden.Contains(order[i]) });
        await db.SaveChangesAsync(ct);
        return await GetLayoutAsync(ct);
    }

    public async Task<DashboardLayoutDto> ResetPreferencesAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        db.DashboardPreferences.RemoveRange(await db.DashboardPreferences.Where(p => p.UserId == userId).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        return await GetLayoutAsync(ct);
    }

    public async Task<WidgetAdminListDto> GetWidgetsAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.WidgetsManage);
        var configs = await db.DashboardWidgets.AsNoTracking().ToDictionaryAsync(c => c.Key, ct);
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.DisplayName).Select(r => new WidgetRoleDto(r.Name, r.DisplayName)).ToListAsync(ct);
        return new WidgetAdminListDto(WidgetCatalog.All.Select(d => ToAdmin(d, configs.GetValueOrDefault(d.Key))).OrderBy(w => w.SortOrder).ToList(), roles);
    }

    private static WidgetAdminDto ToAdmin(WidgetDefinition d, DashboardWidgetConfig? c) => new(
        d.Key, d.Title, d.Description, d.Category, d.RequiredAnyPermission, c?.IsEnabled ?? true, c?.SortOrder ?? d.DefaultOrder,
        c?.Size ?? d.DefaultSize, c?.Roles ?? [], d.DefaultSize, d.DefaultOrder);

    public async Task<WidgetAdminDto> SaveWidgetAsync(string key, SaveWidgetRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.WidgetsManage);
        var def = WidgetCatalog.Find(key) ?? throw NotFoundException.For("Kutucuk", key);
        var roles = (r.Roles ?? []).Distinct().ToList();
        var known = await db.Roles.Where(x => roles.Contains(x.Name)).Select(x => x.Name).ToListAsync(ct);
        if (known.Count != roles.Count) throw new DomainException("Tanımlı olmayan bir rol seçildi.");
        var cfg = await db.DashboardWidgets.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (cfg is null) cfg = db.DashboardWidgets.Add(new DashboardWidgetConfig { Key = key }).Entity;
        cfg.IsEnabled = r.IsEnabled;
        cfg.SortOrder = r.SortOrder;
        cfg.Size = r.Size;
        cfg.Roles = roles;
        cfg.UpdatedAt = DateTime.UtcNow;
        audit.Add("widget.updated", "widget", key, new { r.IsEnabled, r.SortOrder, r.Size, roles });
        await db.SaveChangesAsync(ct);
        return ToAdmin(def, cfg);
    }
}

public sealed class SaveWidgetRequestValidator : AbstractValidator<SaveWidgetRequest>
{
    public SaveWidgetRequestValidator()
    {
        RuleFor(x => x.Size).IsInEnum();
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000);
        RuleFor(x => x.Roles).Must(r => r is null || r.Count <= 20);
    }
}

public sealed class SaveDashboardPreferencesRequestValidator : AbstractValidator<SaveDashboardPreferencesRequest>
{
    public SaveDashboardPreferencesRequestValidator()
    {
        RuleFor(x => x.Order).Must(o => o is null || o.Count <= 100);
        RuleFor(x => x.Hidden).Must(o => o is null || o.Count <= 100);
    }
}
