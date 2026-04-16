using GBILET.Core.DTOs.Common;

namespace GBILET.Core.Interfaces;

public interface ICurrencyService
{
    Task<IReadOnlyList<CurrencyRateDto>> GetAllRatesAsync(CancellationToken ct = default);

     Task<CurrencyRateDto?> GetRateAsync(string currency, CancellationToken ct = default);

    Task<decimal?> ConvertFromTryAsync(decimal amountTry, string targetCurrency, CancellationToken ct = default);


}