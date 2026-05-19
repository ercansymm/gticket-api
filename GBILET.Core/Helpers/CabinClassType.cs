namespace GBILET.Core.Helpers;

public enum CabinClassType
{
    Economy,
    PremiumEconomy,
    Business,
    First
}

public static class CabinClassExtensions
{
    public static string ToDisplayName(this CabinClassType cabinClass) => cabinClass switch
    {
        CabinClassType.Economy => "Ekonomi",
        CabinClassType.PremiumEconomy => "Premium Ekonomi",
        CabinClassType.Business => "Business",
        CabinClassType.First => "First Class",
        _ => cabinClass.ToString()
    };

    public static string ToApiValue(this CabinClassType cabinClass) => cabinClass switch
    {
        CabinClassType.Economy => "Economy",
        CabinClassType.PremiumEconomy => "PremiumEconomy",
        CabinClassType.Business => "Business",
        CabinClassType.First => "First",
        _ => cabinClass.ToString()
    };

    public static CabinClassType ParseCabinClass(string? value)
    {
        if (string.IsNullOrEmpty(value)) return CabinClassType.Economy;

        return value.ToUpperInvariant() switch
        {
            "ECONOMY" or "ECO" => CabinClassType.Economy,
            "PREMIUMECONOMY" or "PEF" => CabinClassType.PremiumEconomy,
            "BUSINESS" or "BUS" => CabinClassType.Business,
            "FIRST" or "FIR" or "FIRSTCLASS" => CabinClassType.First,
            _ => CabinClassType.Economy
        };
    }
}
