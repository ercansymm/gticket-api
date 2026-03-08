namespace GBILET.Core.Models.Flight;

public class AllocateRequest
{
    /// <summary>
    /// Orijinal arama parametreleri (AirSearch'te kullanýlan ayný request tekrar gönderilir)
    /// </summary>
    public SearchRequest SearchRequest { get; set; } = null!;

    /// <summary>
    /// Seçilen uçuþ bilgileri
    /// </summary>
    public SelectedFlight DepartureFlight { get; set; } = null!;

    /// <summary>
    /// Dönüþ uçuþu bilgileri (RT uçuþlarda)
    /// </summary>
    public SelectedFlight? ReturnFlight { get; set; }

    /// <summary>
    /// Servis ücreti (opsiyonel)
    /// </summary>
    public decimal SelectedServiceFee { get; set; } = 0;
}

public class SelectedFlight
{
    /// <summary>
    /// Uçuþ numaralarý (segment baþýna bir tane, ör: ["TK2124", "TK2190"])
    /// </summary>
    public List<string> FlightNumbers { get; set; } = [];

    /// <summary>
    /// Ýþleten havayollarý (segment baþýna bir tane, ör: ["TK", "TK"])
    /// </summary>
    public List<string> OperatingAirlines { get; set; } = [];

    /// <summary>
    /// Saðlayýcý ID (ProviderId - AirSearch yanýtýndaki FlightOption.ProviderId)
    /// </summary>
    public int ProviderId { get; set; }
}
