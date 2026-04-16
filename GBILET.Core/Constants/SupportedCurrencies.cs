namespace GBILET.Core.Constants;

public static class SupportedCurrencies
{
    public const string DefaultCurrency = "TRY";

    public record CurrencyInfo(string Code, string Symbol, string TurkishName);

    public static readonly IReadOnlyList<CurrencyInfo> All = new List<CurrencyInfo>
    {
        new("TRY", "₺", "Türk Lirası"),
        new("USD", "$", "Amerikan Doları"),
        new("EUR", "€", "Euro"),
        new("GBP", "£", "İngiliz Sterlini"),
        new("AZN", "₼", "Azerbaycan Manatı"),
        new("BGN", "лв", "Bulgar Levası"),
        new("GEL", "₾", "Gürcistan Larisi"),
        new("DZD", "د.ج", "Cezayir Dinarı"),
        new("LYD", "ل.د", "Libya Dinarı"),
        new("TND", "د.ت", "Tunus Dinarı")
    };

    public static bool IsSupported(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return All.Any(c => c.Code == code.ToUpperInvariant());
    }

    public static CurrencyInfo? GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return All.FirstOrDefault(c => c.Code == code.ToUpperInvariant());
    }
}