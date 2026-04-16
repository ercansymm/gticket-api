using GBILET.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CurrencyController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public CurrencyController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    /// <summary>
    /// Tum desteklenen para birimleri ve TRY bazli kurlari doner.
    /// Kurlar TCMB'den alinir ve 1 saat cache'lenir.
    /// </summary>
    [HttpGet("rates")]
    public async Task<IActionResult> GetAllRates(CancellationToken ct)
    {
        var rates = await _currencyService.GetAllRatesAsync(ct);
        return Ok(rates);
    }

    /// <summary>
    /// Belirli bir para biriminin TRY bazli kurunu doner.
    /// </summary>
    [HttpGet("rates/{currency}")]
    public async Task<IActionResult> GetRate(string currency, CancellationToken ct)
    {
        var rate = await _currencyService.GetRateAsync(currency, ct);
        if (rate == null)
            return NotFound(new { error = $"'{currency}' para birimi desteklenmiyor veya kur bilgisi bulunamadi." });

        return Ok(rate);
    }

    /// <summary>
    /// TRY tutarini belirtilen para birimine cevirir.
    /// </summary>
    [HttpGet("convert")]
    public async Task<IActionResult> Convert([FromQuery] decimal amount, [FromQuery] string to, CancellationToken ct)
    {
        if (amount < 0)
            return BadRequest(new { error = "Tutar sifir veya daha buyuk olmalidir." });

        if (string.IsNullOrWhiteSpace(to))
            return BadRequest(new { error = "'to' parametresi zorunludur." });

        var converted = await _currencyService.ConvertFromTryAsync(amount, to, ct);
        if (converted == null)
            return NotFound(new { error = $"'{to}' para birimi icin kur bilgisi bulunamadi." });

        var rate = await _currencyService.GetRateAsync(to, ct);

        return Ok(new
        {
            amountTry = amount,
            convertedAmount = converted.Value,
            targetCurrency = to.ToUpperInvariant(),
            rate = rate?.RateTry ?? 0,
            lastUpdatedUtc = rate?.LastUpdatedUtc
        });
    }
}
