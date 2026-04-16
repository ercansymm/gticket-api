namespace GBILET.Core.DTOs.Common;

public record CurrencyRateDto(
    string Currency,
    decimal RateTry,
    DateTime LastUpdatedUtc
);