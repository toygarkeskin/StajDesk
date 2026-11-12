using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Site;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;
using StajDesk.Infrastructure.LiveData;

namespace StajDesk.UnitTests;

/// <summary>Kurumsal site: reklam yayın penceresi, sayaçlar, güvenli bağlantı, haber yayını ve içerik yetkileri.</summary>
public class SiteTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly MemoryStorage _storage = new();
    private readonly User _admin, _hr;

    public SiteTests()
    {
        var roles = _db.SeedRoles();
        _admin = new User { FullName = "Admin", Email = "admin@test" };
        _admin.Roles.Add(new UserRole { Role = roles[RoleNames.SystemAdmin] });
        _hr = new User { FullName = "Deniz", Email = "deniz@test" };
        _hr.Roles.Add(new UserRole { Role = roles[RoleNames.Hr] });
        _db.AddRange(_admin, _hr);
        _db.MediaFiles.Add(new MediaFile { Key = new string('a', 32), ContentType = "image/png", FileName = "a.png" });
        _db.SaveChanges();
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private PublicSiteService Site() => new(_db, _storage, new NoCache(), new FixedClock(Now));
    private ContentAdminService Content() => new(_db, _current, _storage, new NoCache(), new NoAudit(), new FixedClock(Now));

    private Advertisement Ad(string title, int startDays, int? endDays, bool active = true, string? link = "https://ornek.com")
    {
        var ad = new Advertisement
        {
            Title = title, ImageKey = new string('a', 32), LinkUrl = link, Page = AdPage.Landing, Placement = AdPlacement.Hero,
            StartsAt = Now.AddDays(startDays), EndsAt = endDays is null ? null : Now.AddDays(endDays.Value), IsActive = active,
        };
        _db.Advertisements.Add(ad);
        _db.SaveChanges();
        return ad;
    }

    [Fact]
    public async Task Only_live_ads_are_shown_on_the_site()
    {
        Ad("Yayında", -1, 5);
        Ad("Süresi doldu", -10, -1);
        Ad("İleri tarihli", 2, 10);
        Ad("Pasif", -1, 5, active: false);
        Ad("Süresiz", -1, null);

        var ads = await Site().GetAdsAsync(AdPage.Landing);

        Assert.Equal(["Süresiz", "Yayında"], ads.Select(a => a.Title).Order(StringComparer.Ordinal).ToArray());
        Assert.Empty(await Site().GetAdsAsync(AdPage.Portal));
    }

    [Fact]
    public async Task Impressions_and_clicks_are_counted_only_for_live_ads()
    {
        var live = Ad("Yayında", -1, 5);
        var expired = Ad("Süresi doldu", -10, -1);

        await Site().RecordImpressionsAsync([live.Id, live.Id, expired.Id, 999]);
        Assert.Equal("https://ornek.com", await Site().RecordClickAsync(live.Id));
        await Site().RecordClickAsync(expired.Id);

        var stats = _db.AdStats.ToList();
        var row = Assert.Single(stats);
        Assert.Equal(live.Id, row.AdvertisementId);
        Assert.Equal(1, row.Impressions); // aynı istekte tekrarlanan kimlik bir kez sayılır
        Assert.Equal(1, row.Clicks);
    }

    [Fact]
    public async Task Unknown_ad_click_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Site().RecordClickAsync(12345));
    }

    [Theory]
    [InlineData("https://ornek.com/kampanya", true)]
    [InlineData("http://ornek.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,<script>", false)]
    [InlineData("//kotu.com", false)]
    [InlineData("/goreli/adres", false)]
    [InlineData("ftp://ornek.com", false)]
    [InlineData(null, false)]
    public void Only_absolute_http_links_are_safe(string? url, bool safe) => Assert.Equal(safe, UrlRules.IsSafeHttpUrl(url));

    [Fact]
    public void Ad_validator_rejects_script_links_and_inverted_dates()
    {
        var v = new UpsertAdRequestValidator();
        var ok = new UpsertAdRequest("Kampanya", null, new string('a', 32), "https://ornek.com", AdPage.Landing, AdPlacement.Hero, 1, Now, Now.AddDays(3), true);
        Assert.True(v.Validate(ok).IsValid);
        Assert.False(v.Validate(ok with { LinkUrl = "javascript:alert(1)" }).IsValid);
        Assert.False(v.Validate(ok with { EndsAt = Now.AddDays(-1) }).IsValid);
        Assert.False(v.Validate(ok with { Title = "<b>kalın</b>" }).IsValid);
    }

    [Theory]
    [InlineData("Yeni Mağazamız Açıldı!", "yeni-magazamiz-acildi")]
    [InlineData("  İŞ İLANI: Çağrı Merkezi  ", "is-ilani-cagri-merkezi")]
    [InlineData("%%%", "haber")]
    public void Slugify_produces_readable_turkish_slugs(string title, string slug) => Assert.Equal(slug, ContentAdminService.Slugify(title));

    [Fact]
    public async Task Duplicate_news_titles_get_unique_slugs_and_drafts_stay_hidden()
    {
        _current.As(_admin, RoleNames.SystemAdmin);
        var first = await Content().SaveNewsAsync(null, new UpsertNewsRequest("Yeni mağaza", "Özet", "Metin", null, true, Now.AddHours(-1)));
        var second = await Content().SaveNewsAsync(null, new UpsertNewsRequest("Yeni mağaza", "Özet", "Metin", null, false, null));

        Assert.Equal("yeni-magaza", first.Slug);
        Assert.Equal("yeni-magaza-2", second.Slug);
        Assert.Equal("Yeni mağaza", (await Site().GetNewsAsync(first.Slug)).Title);
        await Assert.ThrowsAsync<NotFoundException>(() => Site().GetNewsAsync(second.Slug)); // taslak herkese açık değil
    }

    [Fact]
    public async Task Future_dated_news_is_not_public_yet()
    {
        _current.As(_admin, RoleNames.SystemAdmin);
        var news = await Content().SaveNewsAsync(null, new UpsertNewsRequest("Yarın", "Özet", "Metin", null, true, Now.AddDays(1)));
        await Assert.ThrowsAsync<NotFoundException>(() => Site().GetNewsAsync(news.Slug));
    }

    [Fact]
    public async Task Content_and_ads_require_their_own_permissions()
    {
        _current.As(_hr, RoleNames.Hr);
        await Assert.ThrowsAsync<ForbiddenException>(() => Content().GetAdsAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => Content().SaveNewsAsync(null, new UpsertNewsRequest("Başlık", "Özet", "Metin", null, true, null)));

        // Yalnızca reklam yetkisi olan biri haber yönetemez
        _current.Permissions = new HashSet<string> { Permissions.AdsManage };
        Assert.Empty(await Content().GetAdsAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => Content().GetNewsAsync());
    }

    [Fact]
    public async Task Ads_must_use_an_uploaded_image()
    {
        _current.As(_admin, RoleNames.SystemAdmin);
        var r = new UpsertAdRequest("Kampanya", null, new string('b', 32), null, AdPage.Landing, AdPlacement.Hero, 1, Now, null, true);
        await Assert.ThrowsAsync<StajDesk.Domain.Common.DomainException>(() => Content().SaveAdAsync(null, r));
    }

    [Fact]
    public async Task Media_endpoint_serves_only_registered_files()
    {
        _storage.Files[new string('a', 32)] = [1, 2, 3];
        _storage.Files[new string('c', 32)] = [4, 5, 6]; // örn. talep eki: MediaFiles'ta yok → herkese açık değil
        Assert.NotNull(await Site().GetMediaAsync(new string('a', 32)));
        Assert.Null(await Site().GetMediaAsync(new string('c', 32)));
    }

    /* ---------------- Canlı veri ---------------- */

    private sealed class FlakyRates(bool fail) : IExchangeRateProvider
    {
        public bool Fail { get; set; } = fail;
        public string Source => "TCMB";
        public Task<RatesDto> GetAsync(CancellationToken ct) =>
            Fail ? throw new HttpRequestException("bağlantı yok") : Task.FromResult(new RatesDto("11.11.2026", [new("USD", "ABD DOLARI", 50.1m, 50.2m)]));
    }

    private sealed class NoWeather : IWeatherProvider
    {
        public string? LastCity { get; private set; }
        public string Source => "test";
        public Task<WeatherDto> GetAsync(CityDto city, CancellationToken ct)
        {
            LastCity = city.Name;
            throw new HttpRequestException("yok");
        }
    }

    private readonly NoWeather _weather = new();

    private LiveDataService Live(IExchangeRateProvider rates) =>
        new(_weather, rates, new NotConfiguredGoldProvider(), _db, new NoCache(), NullLogger<LiveDataService>.Instance);

    [Fact]
    public async Task Live_data_falls_back_to_last_successful_value()
    {
        var rates = new FlakyRates(fail: true);
        var first = await Live(rates).RatesAsync();
        Assert.Equal("unavailable", first.Status);
        Assert.Null(first.Data);

        rates.Fail = false;
        var live = await Live(rates).RatesAsync();
        Assert.Equal("live", live.Status);

        rates.Fail = true;
        var stale = await Live(rates).RatesAsync();
        Assert.Equal("stale", stale.Status);
        Assert.Equal(50.2m, stale.Data!.Currencies[0].Selling); // uydurma değil, son gerçek değer
        Assert.NotNull(stale.FetchedAt);
    }

    [Fact]
    public async Task Gold_is_reported_as_not_configured_without_fake_numbers()
    {
        var gold = await Live(new FlakyRates(false)).GoldAsync();
        Assert.Equal("not_configured", gold.Status);
        Assert.Null(gold.Data);
    }

    [Fact]
    public async Task Weather_uses_site_default_city_for_unknown_names()
    {
        _db.SiteSettings.Add(new SiteSetting { Key = SiteKeys.WeatherCity, Value = "Ankara" });
        _db.SaveChanges();
        var w = await Live(new FlakyRates(false)).WeatherAsync("Atlantis");
        Assert.Equal("unavailable", w.Status);
        Assert.Equal("Ankara", _weather.LastCity);
        Assert.Empty(_db.ExternalSnapshots); // hata durumunda anlık görüntü yazılmaz
    }

    [Fact]
    public void Tcmb_bulletin_is_parsed()
    {
        var xml = XDocument.Parse("""
            <Tarih_Date Tarih="11.11.2026" Date="11/11/2026">
              <Currency CrossOrder="0" Kod="USD" CurrencyCode="USD"><Unit>1</Unit><Isim>ABD DOLARI</Isim><ForexBuying>50.1379</ForexBuying><ForexSelling>50.2282</ForexSelling></Currency>
              <Currency CrossOrder="1" Kod="AUD" CurrencyCode="AUD"><Unit>1</Unit><Isim>AVUSTRALYA DOLARI</Isim><ForexBuying>32.1</ForexBuying><ForexSelling>32.3</ForexSelling></Currency>
              <Currency CrossOrder="9" Kod="EUR" CurrencyCode="EUR"><Unit>1</Unit><Isim>EURO</Isim><ForexBuying>55.8804</ForexBuying><ForexSelling>55.9810</ForexSelling></Currency>
            </Tarih_Date>
            """);
        var r = TcmbExchangeRateProvider.Parse(xml);
        Assert.Equal("11.11.2026", r.BulletinDate);
        Assert.Equal(["USD", "EUR"], r.Currencies.Select(c => c.Code).ToArray());
        Assert.Equal(55.9810m, r.Currencies[1].Selling);
    }

    [Fact]
    public void Tcmb_parse_fails_loudly_when_rates_are_missing()
    {
        Assert.Throws<FormatException>(() => TcmbExchangeRateProvider.Parse(XDocument.Parse("<Tarih_Date Tarih=\"x\"/>")));
    }

    [Fact]
    public void Open_meteo_response_is_parsed()
    {
        using var doc = JsonDocument.Parse("""
            {"current":{"temperature_2m":20.2,"relative_humidity_2m":70,"wind_speed_10m":26.4,"weather_code":2},
             "daily":{"time":["2026-11-12","2026-11-13"],"weather_code":[3,61],"temperature_2m_max":[20.5,18.0],"temperature_2m_min":[16.6,12.1]}}
            """);
        var w = OpenMeteoWeatherProvider.Parse("İstanbul", doc);
        Assert.Equal(20.2, w.Temperature);
        Assert.Equal(70, w.Humidity);
        Assert.Equal(2, w.Daily.Count);
        Assert.Equal(new DateOnly(2026, 11, 13), w.Daily[1].Date);
        Assert.Equal(WeatherCodes.Describe(61), w.Daily[1].Description);
    }
}

