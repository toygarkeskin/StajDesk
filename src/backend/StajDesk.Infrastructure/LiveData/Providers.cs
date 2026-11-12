using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using StajDesk.Application.Features.Site;

namespace StajDesk.Infrastructure.LiveData;

/// <summary>
/// Dış servisler için tek paylaşılan HttpClient: kısa zaman aşımı (servis yavaşsa sayfa beklemez, son başarılı veri gösterilir)
/// ve DNS değişikliklerini yakalamak için bağlantı ömrü sınırı.
/// </summary>
public sealed class LiveDataHttp
{
    public HttpClient Client { get; } = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) })
    {
        Timeout = TimeSpan.FromSeconds(12),
        DefaultRequestHeaders =
        {
            // TCMB, tarayıcı benzeri User-Agent göndermeyen istekleri yavaşlatıyor / kesiyor.
            { "User-Agent", "Mozilla/5.0 (compatible; StajDesk/1.0)" },
        },
    };
}

/// <summary>
/// Open-Meteo hava durumu (anahtar gerektirmez). Not: ücretsiz kullanım ticari olmayan amaçlar içindir;
/// şirket kullanımında Open-Meteo'nun ücretli planı veya başka bir sağlayıcı yapılandırılmalıdır (bkz. docs/KURUMSAL_SITE.md).
/// </summary>
internal sealed class OpenMeteoWeatherProvider(LiveDataHttp http) : IWeatherProvider
{
    public string Source => "Open-Meteo";

    public async Task<WeatherDto> GetAsync(CityDto city, CancellationToken ct)
    {
        var lat = city.Latitude.ToString(CultureInfo.InvariantCulture);
        var lon = city.Longitude.ToString(CultureInfo.InvariantCulture);
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                  "&current=temperature_2m,relative_humidity_2m,wind_speed_10m,weather_code" +
                  "&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=Europe%2FIstanbul&forecast_days=4";
        using var res = await http.Client.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        return Parse(city.Name, await JsonDocument.ParseAsync(stream, cancellationToken: ct));
    }

    public static WeatherDto Parse(string city, JsonDocument doc)
    {
        var current = doc.RootElement.GetProperty("current");
        var daily = doc.RootElement.GetProperty("daily");
        var days = daily.GetProperty("time").EnumerateArray().Select((t, i) =>
        {
            var code = daily.GetProperty("weather_code")[i].GetInt32();
            return new DailyForecastDto(DateOnly.Parse(t.GetString()!, CultureInfo.InvariantCulture), code, WeatherCodes.Describe(code),
                daily.GetProperty("temperature_2m_max")[i].GetDouble(), daily.GetProperty("temperature_2m_min")[i].GetDouble());
        }).ToList();
        var nowCode = current.GetProperty("weather_code").GetInt32();
        return new WeatherDto(city, current.GetProperty("temperature_2m").GetDouble(), nowCode, WeatherCodes.Describe(nowCode),
            (int)Math.Round(current.GetProperty("relative_humidity_2m").GetDouble()), current.GetProperty("wind_speed_10m").GetDouble(), days);
    }
}

/// <summary>TCMB günlük gösterge niteliğindeki kurlar (today.xml). İş günleri 15:30'da güncellenir; hafta sonu son iş gününün bülteni geçerlidir.</summary>
internal sealed class TcmbExchangeRateProvider(LiveDataHttp http) : IExchangeRateProvider
{
    private static readonly string[] Codes = ["USD", "EUR", "GBP"];
    public string Source => "TCMB";

    public async Task<RatesDto> GetAsync(CancellationToken ct)
    {
        using var res = await http.Client.GetAsync("https://www.tcmb.gov.tr/kurlar/today.xml", ct);
        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        return Parse(await XDocument.LoadAsync(stream, LoadOptions.None, ct));
    }

    public static RatesDto Parse(XDocument doc)
    {
        var root = doc.Root ?? throw new FormatException("TCMB yanıtı boş.");
        decimal D(XElement c, string name) =>
            decimal.Parse(c.Element(name)?.Value ?? throw new FormatException($"{name} yok"), NumberStyles.Number, CultureInfo.InvariantCulture);
        var list = root.Elements("Currency")
            .Where(c => Codes.Contains((string?)c.Attribute("CurrencyCode")))
            .Select(c => new CurrencyRateDto((string)c.Attribute("CurrencyCode")!, c.Element("Isim")?.Value ?? "", D(c, "ForexBuying"), D(c, "ForexSelling")))
            .OrderBy(c => Array.IndexOf(Codes, c.Code)).ToList();
        if (list.Count == 0) throw new FormatException("TCMB yanıtında beklenen kurlar yok.");
        return new RatesDto((string?)root.Attribute("Tarih") ?? "", list);
    }
}

/// <summary>Gram altın: ücretsiz ve resmî kaynak olmadığı için varsayılan olarak yapılandırılmamış. Sahte veri üretmez.</summary>
internal sealed class NotConfiguredGoldProvider : IGoldPriceProvider
{
    public bool IsConfigured => false;
    public string Source => "yapılandırılmadı";
    public Task<GoldDto> GetAsync(CancellationToken ct) => throw new InvalidOperationException("Gram altın sağlayıcısı yapılandırılmadı.");
}
