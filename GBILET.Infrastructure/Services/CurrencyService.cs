using System.Globalization;
using System.Xml.Linq;
using GBILET.Core.DTOs.Common;
using GBILET.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GBILET.Infrastructure.Services;

public class CurrencyService : ICurrencyService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CurrencyService> _logger;

    private const string CacheKey = "tcmb_currency_rates";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private const string TcmbUrl = "https://www.tcmb.gov.tr/kurlar/today.xml";
    private const string FallbackApiUrl = "https://open.er-api.com/v6/latest/TRY";

    /// <summary>
    /// Desteklenen para birimleri. TRY haric — TRY icin rate her zaman 1.
    /// </summary>
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "EUR", "USD", "GBP", "AZN", "BGN", "DZD", "GEL", "LYD", "TND"
    };

    public CurrencyService(HttpClient httpClient, IMemoryCache cache, ILogger<CurrencyService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CurrencyRateDto>> GetAllRatesAsync(CancellationToken ct = default)
    {
        var rates = await GetCachedRatesAsync(ct);
        return rates;
    }

    public async Task<CurrencyRateDto?> GetRateAsync(string currency, CancellationToken ct = default)
    {
        if (string.Equals(currency, "TRY", StringComparison.OrdinalIgnoreCase))
            return new CurrencyRateDto("TRY", 1m, DateTime.UtcNow);

        var rates = await GetCachedRatesAsync(ct);
        return rates.FirstOrDefault(r => string.Equals(r.Currency, currency, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<decimal?> ConvertFromTryAsync(decimal amountTry, string targetCurrency, CancellationToken ct = default)
    {
        if (string.Equals(targetCurrency, "TRY", StringComparison.OrdinalIgnoreCase))
            return amountTry;

        var rate = await GetRateAsync(targetCurrency, ct);
        if (rate == null || rate.RateTry <= 0)
            return null;

        // 1 USD = 44.7 TRY  →  amountTry / rateTry = amountInCurrency
        return Math.Round(amountTry / rate.RateTry, 2);
    }

    private async Task<IReadOnlyList<CurrencyRateDto>> GetCachedRatesAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue<IReadOnlyList<CurrencyRateDto>>(CacheKey, out var cached) && cached != null)
            return cached;

        var rates = await FetchRatesAsync(ct);

        _cache.Set(CacheKey, rates, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheDuration));

        return rates;
    }

    private async Task<IReadOnlyList<CurrencyRateDto>> FetchRatesAsync(CancellationToken ct)
    {
        var result = new List<CurrencyRateDto>
        {
            // TRY her zaman 1:1
            new("TRY", 1m, DateTime.UtcNow)
        };

        // 1. TCMB XML'den kurlari cek
        var tcmbRates = await FetchTcmbRatesAsync(ct);
        var resolvedCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TRY" };

        foreach (var (code, rate) in tcmbRates)
        {
            if (SupportedCurrencies.Contains(code))
            {
                result.Add(new CurrencyRateDto(code.ToUpperInvariant(), rate, DateTime.UtcNow));
                resolvedCurrencies.Add(code);
            }
        }

        // 2. TCMB'de olmayan para birimleri icin fallback API
        var missing = SupportedCurrencies.Where(c => !resolvedCurrencies.Contains(c)).ToList();
        if (missing.Count > 0)
        {
            _logger.LogInformation("[CurrencyService] TCMB'de bulunamayan para birimleri: {Missing}. Fallback API kullaniliyor.",
                string.Join(", ", missing));

            var fallbackRates = await FetchFallbackRatesAsync(missing, ct);
            foreach (var (code, rate) in fallbackRates)
            {
                result.Add(new CurrencyRateDto(code.ToUpperInvariant(), rate, DateTime.UtcNow));
            }
        }

        _logger.LogInformation("[CurrencyService] {Count} para birimi yuklendi: {Currencies}",
            result.Count, string.Join(", ", result.Select(r => $"{r.Currency}={r.RateTry:F4}")));

        return result.AsReadOnly();
    }

    /// <summary>
    /// TCMB gunluk kur XML'ini parse eder.
    /// Her Currency elementi: CurrencyCode, Unit, ForexSelling (= 1 birim doviz kac TRY).
    /// </summary>
    private async Task<List<(string Code, decimal Rate)>> FetchTcmbRatesAsync(CancellationToken ct)
    {
        var rates = new List<(string, decimal)>();

        try
        {
            var xml = await _httpClient.GetStringAsync(TcmbUrl, ct);
            var doc = XDocument.Parse(xml);

            foreach (var currency in doc.Descendants("Currency"))
            {
                var code = currency.Attribute("CurrencyCode")?.Value;
                if (string.IsNullOrEmpty(code))
                    continue;

                var unitStr = currency.Element("Unit")?.Value;
                var forexSellingStr = currency.Element("ForexSelling")?.Value;

                if (string.IsNullOrWhiteSpace(forexSellingStr) || string.IsNullOrWhiteSpace(unitStr))
                    continue;

                if (!int.TryParse(unitStr, out var unit) || unit <= 0)
                    continue;

                if (!decimal.TryParse(forexSellingStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var forexSelling) || forexSelling <= 0)
                    continue;

                // ForexSelling = `unit` birim doviz kac TRY
                // Biz 1 birim doviz = kac TRY istiyoruz
                var ratePerUnit = forexSelling / unit;
                rates.Add((code, ratePerUnit));
            }

            _logger.LogInformation("[CurrencyService] TCMB'den {Count} kur alindi.", rates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CurrencyService] TCMB kur verisi alinamadi.");
        }

        return rates;
    }

    /// <summary>
    /// TCMB'de olmayan para birimleri icin open.er-api.com fallback.
    /// Bu API 1 TRY = X yabanci para seklinde doner; biz 1 yabanci = 1/X TRY'ye ceviriyoruz.
    /// </summary>
    private async Task<List<(string Code, decimal Rate)>> FetchFallbackRatesAsync(List<string> currencies, CancellationToken ct)
    {
        var rates = new List<(string, decimal)>();

        try
        {
            var json = await _httpClient.GetStringAsync(FallbackApiUrl, ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("rates", out var ratesObj))
            {
                _logger.LogWarning("[CurrencyService] Fallback API'den rates alani bulunamadi.");
                return rates;
            }

            foreach (var code in currencies)
            {
                if (ratesObj.TryGetProperty(code.ToUpperInvariant(), out var rateEl))
                {
                    var rateFromTry = rateEl.GetDecimal(); // 1 TRY = X foreign
                    if (rateFromTry > 0)
                    {
                        var rateTry = 1m / rateFromTry; // 1 foreign = 1/X TRY
                        rates.Add((code, Math.Round(rateTry, 6)));
                    }
                }
                else
                {
                    _logger.LogWarning("[CurrencyService] Fallback API'de {Currency} bulunamadi.", code);
                }
            }

            _logger.LogInformation("[CurrencyService] Fallback API'den {Count} kur alindi.", rates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CurrencyService] Fallback kur API'si basarisiz.");
        }

        return rates;
    }
}
