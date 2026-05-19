namespace GBILET.Core.Models.Flight;

/// <summary>
/// AirSearch memory cache yapilandirma seceneklerini barindiran konfigurasyon sinifi.
/// appsettings.json icindeki "FlightCache" bolumune bagli olarak okunur.
/// </summary>
public class FlightCacheOptions
{
    public const string SectionName = "FlightCache";

    /// <summary>Sliding expiration suresi (dakika). Default 5.</summary>
    public int SlidingMinutes { get; set; } = 5;

    /// <summary>Absolute expiration suresi (dakika). Default 15. BiletBank session ~20dk oldugundan altinda olmalidir.</summary>
    public int AbsoluteMinutes { get; set; } = 15;

    /// <summary>IMemoryCache size limit. Default 10000 entry.</summary>
    public long SizeLimit { get; set; } = 10000;

    /// <summary>Cache key version prefix. Schema degisikliginde bumplenir.</summary>
    public string KeyVersion { get; set; } = "v1";
}
