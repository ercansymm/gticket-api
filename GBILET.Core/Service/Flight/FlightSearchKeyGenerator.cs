using System.Globalization;
using GBILET.Core.Models.Flight;

namespace GBILET.Core.Service.Flight;

/// <summary>
/// AirSearch cache anahtari ureten yardimci sinif. Musteri segmenti, kupon kodu
/// veya provider adi anahtara dahil edilmez (single provider, runtime uygulanan
/// indirimler cache disinda hesaplandigi icin).
/// </summary>
public static class FlightSearchKeyGenerator
{
    /// <summary>Format: flight_search:{version}:{org}_{dest}_{depDate}_{retDate|OW}_{aAcCcIi}_{cabin}_{currency}</summary>
    public static string Build(SearchRequest request, string keyVersion = "v1", string currency = "TRY")
    {
        ArgumentNullException.ThrowIfNull(request);

        var origin = Norm(request.Origin);
        var destination = Norm(request.Destination);
        var depDate = request.DepartureDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var retDate = request.ReturnDate.HasValue
            ? request.ReturnDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            : "OW";
        var paxBlock = $"{request.AdultCount}A{request.ChildCount}C{request.InfantCount}I";
        var cabin = Norm(request.FlightClass);
        var cur = Norm(currency);

        return $"flight_search:{keyVersion}:{origin}_{destination}_{depDate}_{retDate}_{paxBlock}_{cabin}_{cur}";
    }

    private static string Norm(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}
