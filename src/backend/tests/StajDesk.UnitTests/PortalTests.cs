using StajDesk.Application.Common;
using StajDesk.Application.Features.Portal;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>Aşama 5: widget sistemi (izin + rol + kişisel düzen), genel arama, doğum günü, yeni başlayanlar, takdir ve etkinlik yetkileri.</summary>
public class PortalTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc); // İstanbul 12:00
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly User _admin, _hr, _user, _agent, _other;
    private readonly Category _category;

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public PortalTests()
    {
        var roles = _db.SeedRoles();
        User U(string name, string role, int? storeId = null)
        {
            var u = new User { FullName = name, Email = $"{name}@test", EmailVerifiedAt = Now, StoreId = storeId };
            u.Roles.Add(new UserRole { Role = roles[role] });
            return u;
        }
        var s1 = new Store { Code = "S1", Name = "Kadıköy" };
        var s2 = new Store { Code = "S2", Name = "Kızılay" };
        _db.AddRange(s1, s2);
        _db.SaveChanges();
        _admin = U("Admin", RoleNames.SystemAdmin);
        _hr = U("Deniz", RoleNames.Hr);
        _user = U("Ayşe", RoleNames.User, s1.Id);
        _agent = U("Elif", RoleNames.Agent);
        _other = U("Zeynep", RoleNames.User, s2.Id);
        _category = new Category { Name = "Donanım" };
        _db.AddRange(_admin, _hr, _user, _agent, _other, _category);
        _db.SaveChanges();
    }

    private DashboardService Dashboard() => new(_db, _current, new NoAudit());
    private PortalService Portal() => new(_db, _current, new NoAudit(), new FixedClock(Now));

    // ---------- Widget sistemi ----------

    [Fact]
    public async Task Widgets_requiring_a_permission_are_never_shown_without_it()
    {
        _current.As(_user, RoleNames.User);
        var keys = (await Dashboard().GetLayoutAsync()).Widgets.Select(w => w.Key).ToList();
        Assert.Contains("welcome", keys);
        Assert.DoesNotContain("people-search", keys);    // tickets/users/assets.manage gerekir
        Assert.DoesNotContain("pending-accounts", keys); // hesap talebi yetkisi gerekir

        // Yönetici kutucuğu "herkese" açsa bile izin kuralı gevşemez
        _current.As(_admin, RoleNames.SystemAdmin);
        await Dashboard().SaveWidgetAsync("people-search", new SaveWidgetRequest(true, 1, WidgetSize.Large, []));
        _current.As(_user, RoleNames.User);
        Assert.DoesNotContain("people-search", (await Dashboard().GetLayoutAsync()).Widgets.Select(w => w.Key));
    }

    [Fact]
    public async Task Admin_can_disable_resize_and_target_widgets_by_role()
    {
        _current.As(_admin, RoleNames.SystemAdmin);
        await Dashboard().SaveWidgetAsync("gold", new SaveWidgetRequest(false, 70, WidgetSize.Small, []));
        await Dashboard().SaveWidgetAsync("oncall", new SaveWidgetRequest(true, 1, WidgetSize.Medium, [RoleNames.Agent]));

        _current.As(_user, RoleNames.User);
        var userKeys = (await Dashboard().GetLayoutAsync()).Widgets.Select(w => w.Key).ToList();
        Assert.DoesNotContain("gold", userKeys);
        Assert.DoesNotContain("oncall", userKeys); // yalnızca destek personeline hedeflendi

        _current.As(_agent, RoleNames.Agent);
        var agent = (await Dashboard().GetLayoutAsync()).Widgets;
        Assert.Equal("oncall", agent[0].Key);              // yönetici sırası
        Assert.Equal(WidgetSize.Medium, agent[0].Size);    // yönetici boyutu
    }

    [Fact]
    public async Task Only_widget_managers_can_change_company_widgets()
    {
        _current.As(_hr, RoleNames.Hr);
        await Assert.ThrowsAsync<ForbiddenException>(() => Dashboard().GetWidgetsAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => Dashboard().SaveWidgetAsync("gold", new SaveWidgetRequest(false, 1, WidgetSize.Small, [])));
        _current.As(_admin, RoleNames.SystemAdmin);
        await Assert.ThrowsAsync<DomainException>(() => Dashboard().SaveWidgetAsync("gold", new SaveWidgetRequest(true, 1, WidgetSize.Small, ["olmayan_rol"])));
        await Assert.ThrowsAsync<NotFoundException>(() => Dashboard().SaveWidgetAsync("yok", new SaveWidgetRequest(true, 1, WidgetSize.Small, [])));
    }

    [Fact]
    public async Task Users_personalise_order_and_hidden_widgets_and_can_reset()
    {
        _current.As(_user, RoleNames.User);
        var layout = await Dashboard().SavePreferencesAsync(new SaveDashboardPreferencesRequest(["rates", "welcome", "people-search", "uydurma"], ["gold"]));
        Assert.Equal(["rates", "welcome"], layout.Widgets.Take(2).Select(w => w.Key).ToArray());
        Assert.Contains(layout.Hidden, w => w.Key == "gold");
        Assert.DoesNotContain(layout.Widgets, w => w.Key == "gold");
        Assert.DoesNotContain(_db.DashboardPreferences, p => p.WidgetKey is "people-search" or "uydurma"); // yetkisiz/bilinmeyen kaydedilmez
        Assert.True(layout.Customized);

        // Başka kullanıcının düzeni etkilenmez
        _current.As(_other, RoleNames.User);
        Assert.Equal("welcome", (await Dashboard().GetLayoutAsync()).Widgets[0].Key);

        _current.As(_user, RoleNames.User);
        var reset = await Dashboard().ResetPreferencesAsync();
        Assert.False(reset.Customized);
        Assert.Empty(reset.Hidden);
    }

    // ---------- Akışlar ----------

    private void Profile(User u, DateOnly? birth = null, DateOnly? hire = null, bool share = false)
    {
        _db.EmployeeProfiles.Add(new EmployeeProfile { UserId = u.Id, BirthDate = birth, HireDate = hire });
        _db.UserSettings.Add(new UserSettings { UserId = u.Id, ShowBirthday = share });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Birthdays_show_only_opted_in_people_within_seven_days_without_year()
    {
        Profile(_user, birth: new DateOnly(1990, 11, 12), share: true);   // bugün
        Profile(_agent, birth: new DateOnly(1985, 11, 18), share: true);  // 6 gün sonra
        Profile(_other, birth: new DateOnly(1992, 11, 13), share: false); // paylaşmıyor
        Profile(_hr, birth: new DateOnly(1980, 12, 25), share: true);     // 7 günden uzak
        _current.As(_admin, RoleNames.SystemAdmin);

        var list = await Portal().BirthdaysAsync();

        Assert.Equal(["Ayşe", "Elif"], list.Select(b => b.Person.FullName).ToArray());
        Assert.True(list[0].IsToday);
        Assert.Equal((11, 18), (list[1].Month, list[1].Day));
    }

    [Fact]
    public async Task New_hires_are_last_thirty_days_and_active_only()
    {
        Profile(_user, hire: new DateOnly(2026, 11, 2));
        Profile(_agent, hire: new DateOnly(2026, 9, 1));   // eski
        Profile(_other, hire: new DateOnly(2026, 12, 1));  // henüz başlamadı
        _hr.IsActive = false;
        Profile(_hr, hire: new DateOnly(2026, 11, 5));     // pasif hesap
        _current.As(_admin, RoleNames.SystemAdmin);

        Assert.Equal(["Ayşe"], (await Portal().NewHiresAsync()).Select(n => n.Person.FullName).ToArray());
    }

    [Fact]
    public async Task Search_respects_ticket_and_announcement_scope()
    {
        _db.Tickets.AddRange(new Ticket("Yazıcı arızası Kadıköy", "x", _user.Id, _category.Id, storeId: _user.StoreId),
                             new Ticket("Yazıcı arızası Kızılay", "x", _other.Id, _category.Id, storeId: _other.StoreId));
        _db.Announcements.AddRange(
            new Announcement { Title = "Yazıcı bakımı (Kızılay)", Body = "x", AuthorId = _admin.Id, PublishAt = Now.AddDays(-1), StoreId = _other.StoreId },
            new Announcement { Title = "Yazıcı bakımı (genel)", Body = "x", AuthorId = _admin.Id, PublishAt = Now.AddDays(-1) });
        _db.SaveChanges();

        _current.As(_user, RoleNames.User);
        var r = await Portal().SearchAsync("yazıcı");
        Assert.Equal(["Yazıcı arızası Kadıköy"], r.Tickets.Select(t => t.Title).ToArray()); // yalnızca kendi talebi
        Assert.Equal(["Yazıcı bakımı (genel)"], r.Announcements.Select(a => a.Title).ToArray()); // başka mağazanın duyurusu yok

        _current.As(_admin, RoleNames.SystemAdmin);
        Assert.Equal(2, (await Portal().SearchAsync("yazıcı")).Tickets.Count);
    }

    [Fact]
    public async Task Search_finds_people_but_not_inactive_accounts_and_ignores_short_terms()
    {
        _other.IsActive = false;
        _db.SaveChanges();
        _current.As(_user, RoleNames.User);
        Assert.Contains((await Portal().SearchAsync("elif")).People, p => p.FullName == "Elif");
        Assert.Empty((await Portal().SearchAsync("zeynep")).People);
        Assert.Empty((await Portal().SearchAsync("a")).People);
    }

    [Fact]
    public async Task Recognitions_and_events_require_their_permissions()
    {
        _current.As(_user, RoleNames.User);
        await Assert.ThrowsAsync<ForbiddenException>(() => Portal().SaveRecognitionAsync(null, new UpsertRecognitionRequest(_agent.Id, RecognitionType.EmployeeOfWeek, "Haftanın çalışanı", null, new DateOnly(2026, 11, 10))));
        await Assert.ThrowsAsync<ForbiddenException>(() => Portal().SaveEventAsync(null, new UpsertCompanyEventRequest("Toplantı", null, null, Now.AddDays(1), null, true)));

        _current.As(_hr, RoleNames.Hr); // İK takdir girebilir, etkinlik yönetemez
        await Portal().SaveRecognitionAsync(null, new UpsertRecognitionRequest(_agent.Id, RecognitionType.EmployeeOfWeek, "Haftanın çalışanı", null, new DateOnly(2026, 11, 10)));
        await Assert.ThrowsAsync<ForbiddenException>(() => Portal().SaveEventAsync(null, new UpsertCompanyEventRequest("Toplantı", null, null, Now.AddDays(1), null, true)));

        _current.As(_admin, RoleNames.SystemAdmin);
        await Portal().SaveEventAsync(null, new UpsertCompanyEventRequest("Gelecek", null, null, Now.AddDays(2), null, true));
        await Portal().SaveEventAsync(null, new UpsertCompanyEventRequest("Geçmiş", null, null, Now.AddDays(-2), null, true));
        await Portal().SaveEventAsync(null, new UpsertCompanyEventRequest("Taslak", null, null, Now.AddDays(3), null, false));

        _current.As(_user, RoleNames.User);
        Assert.Equal(["Gelecek"], (await Portal().UpcomingEventsAsync()).Select(e => e.Title).ToArray());
        Assert.Equal("Elif", Assert.Single(await Portal().RecentRecognitionsAsync()).Person.FullName);
    }
}
