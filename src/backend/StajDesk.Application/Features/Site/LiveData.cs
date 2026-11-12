using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Domain.Entities;

namespace StajDesk.Application.Features.Site;

/* =========================================================
   Canlı veri: hava durumu, döviz, gram altın.
   Kurallar (portal promptu §7):
   - Dış servis çağrıları yalnızca backend'den; anahtar varsa frontend'e gitmez.
   - Redis önbelleği; başarılı veri "son başarılı veri" olarak veritabanına da yazılır.
   - Kaynak çökerse son başarılı veri "eski" (stale) işaretiyle ve alınma zamanıyla gösterilir.
   - Kaynak yapılandırılmadıysa (gram altın) "not_configured" döner; sahte rakam ASLA üretilmez.
   ========================================================= */

/// <summary>status: live | stale | not_configured | unavailable</summary>
public sealed record LiveValue<T>(string Status, T? Data, string Source, DateTime? FetchedAt, string? Note);

public sealed record DailyForecastDto(DateOnly Date, int WeatherCode, string Description, double Max, double Min);
public sealed record WeatherDto(string City, double Temperature, int WeatherCode, string Description, int Humidity, double WindKmh, IReadOnlyList<DailyForecastDto> Daily);
public sealed record CurrencyRateDto(string Code, string Name, decimal Buying, decimal Selling);
public sealed record RatesDto(string BulletinDate, IReadOnlyList<CurrencyRateDto> Currencies);
public sealed record GoldDto(decimal GramBuying, decimal GramSelling);
public sealed record CityDto(string Name, double Latitude, double Longitude);

/// <summary>Hava durumu sağlayıcısı (ör. Open-Meteo). Hata durumunda istisna fırlatır.</summary>
public interface IWeatherProvider
{
    string Source { get; }
    Task<WeatherDto> GetAsync(CityDto city, CancellationToken ct);
}

/// <summary>Döviz kuru sağlayıcısı (ör. TCMB günlük bülten).</summary>
public interface IExchangeRateProvider
{
    string Source { get; }
    Task<RatesDto> GetAsync(CancellationToken ct);
}

/// <summary>Gram altın sağlayıcısı. Ücretsiz resmî kaynak olmadığından varsayılan uygulama yapılandırılmamıştır.</summary>
public interface IGoldPriceProvider
{
    bool IsConfigured { get; }
    string Source { get; }
    Task<GoldDto> GetAsync(CancellationToken ct);
}

public interface ILiveDataService
{
    IReadOnlyList<CityDto> Cities { get; }
    Task<LiveValue<WeatherDto>> WeatherAsync(string? city, CancellationToken ct = default);
    Task<LiveValue<RatesDto>> RatesAsync(CancellationToken ct = default);
    Task<LiveValue<GoldDto>> GoldAsync(CancellationToken ct = default);
}

public static class WeatherCodes
{
    /// <summary>WMO hava kodu → Türkçe açıklama.</summary>
    public static string Describe(int code) => code switch
    {
        0 => "Açık",
        1 => "Çoğunlukla açık",
        2 => "Parçalı bulutlu",
        3 => "Kapalı",
        45 or 48 => "Sisli",
        51 or 53 or 55 => "Çisenti",
        56 or 57 => "Dondurucu çisenti",
        61 or 63 => "Yağmurlu",
        65 => "Kuvvetli yağmur",
        66 or 67 => "Dondurucu yağmur",
        71 or 73 or 75 or 77 => "Kar yağışlı",
        80 or 81 or 82 => "Sağanak yağışlı",
        85 or 86 => "Kar sağanağı",
        95 => "Gök gürültülü fırtına",
        96 or 99 => "Dolu ile fırtına",
        _ => "Bilinmiyor",
    };
}

internal sealed class LiveDataService(
    IWeatherProvider weather, IExchangeRateProvider rates, IGoldPriceProvider gold,
    IAppDbContext db, ICacheService cache, ILogger<LiveDataService> logger) : ILiveDataService
{
    private static readonly TimeSpan WeatherTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RatesTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FailureTtl = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<CityDto> Cities { get; } =
    [
        new("İstanbul", 41.01, 28.98), new("Ankara", 39.93, 32.86), new("İzmir", 38.42, 27.14), new("Bursa", 40.19, 29.06),
        new("Antalya", 36.90, 30.70), new("Adana", 37.00, 35.32), new("Konya", 37.87, 32.48), new("Gaziantep", 37.06, 37.38),
        new("Kayseri", 38.72, 35.49), new("Mersin", 36.81, 34.64), new("Eskişehir", 39.78, 30.52), new("Samsun", 41.29, 36.33),
        new("Trabzon", 41.00, 39.72), new("Diyarbakır", 37.91, 40.24),
    ];

    public async Task<LiveValue<WeatherDto>> WeatherAsync(string? cityName, CancellationToken ct = default)
    {
        var defaultCity = await db.SiteSettings.AsNoTracking().Where(s => s.Key == SiteKeys.WeatherCity).Select(s => s.Value).FirstOrDefaultAsync(ct);
        var city = Cities.FirstOrDefault(c => string.Equals(c.Name, cityName, StringComparison.OrdinalIgnoreCase))
                   ?? Cities.FirstOrDefault(c => c.Name == defaultCity) ?? Cities[0];
        return await FetchAsync($"weather:{city.Name}", weather.Source, token => weather.GetAsync(city, token), WeatherTtl, ct);
    }

    public Task<LiveValue<RatesDto>> RatesAsync(CancellationToken ct = default) =>
        FetchAsync("rates:tcmb", rates.Source, rates.GetAsync, RatesTtl, ct);

    public Task<LiveValue<GoldDto>> GoldAsync(CancellationToken ct = default) =>
        gold.IsConfigured
            ? FetchAsync("gold", gold.Source, gold.GetAsync, RatesTtl, ct)
            : Task.FromResult(new LiveValue<GoldDto>("not_configured", null, gold.Source, null,
                "Gram altın için ücretsiz ve resmî bir veri kaynağı yok. Sağlayıcı anahtarı tanımlandığında görünür."));

    /// <summary>Önbellek → canlı kaynak → (hata) son başarılı veri → (yoksa) "unavailable".</summary>
    private Task<LiveValue<T>> FetchAsync<T>(string key, string source, Func<CancellationToken, Task<T>> fetch, TimeSpan ttl, CancellationToken ct) =>
        cache.GetOrSetAsync($"live:{key}", async token =>
        {
            try
            {
                var data = await fetch(token);
                await SaveSnapshotAsync(key, source, data, token);
                return new LiveValue<T>("live", data, source, DateTime.UtcNow, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
            {
                logger.LogWarning("Canlı veri alınamadı ({Key}, {Source}): {Message}", key, source, ex.Message);
                var snap = await db.ExternalSnapshots.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, token);
                return snap is null
                    ? new LiveValue<T>("unavailable", default, source, null, "Kaynağa şu anda ulaşılamıyor.")
                    : new LiveValue<T>("stale", JsonSerializer.Deserialize<T>(snap.Payload, Json), snap.Source, snap.FetchedAt,
                        "Kaynağa şu anda ulaşılamıyor; son başarılı veri gösteriliyor.");
            }
        }, v => v.Status == "live" ? ttl : FailureTtl, ct);

    private async Task SaveSnapshotAsync<T>(string key, string source, T data, CancellationToken ct)
    {
        var snap = await db.ExternalSnapshots.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (snap is null) snap = db.ExternalSnapshots.Add(new ExternalSnapshot { Key = key }).Entity;
        snap.Source = source;
        snap.Payload = JsonSerializer.Serialize(data, Json);
        snap.FetchedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
