namespace GBILET.Core.Models.Flight;

public class AllocateRequest
{
    /// <summary>
    /// Search response'tan alinan SessionId.
    /// Bu deger verilirse tekrar login+search yapilmaz, dogrudan allocate yapilir.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Search response'tan alinan SessionToken.
    /// </summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// Orijinal arama parametreleri.
    /// SessionId/SessionToken verilmemisse login+search yapilir.
    /// </summary>
    public SearchRequest? SearchRequest { get; set; }

    /// <summary>
    /// Search sonucundan secilen urun ID'si (FlightOption.ProductId)
    /// </summary>
    public string ProductId { get; set; } = null!;

    /// <summary>
    /// Servis ucreti / satici komisyonu. Komisyon hesaplamasi gerekmiyorsa 0 gonderin.
    /// Response'ta LastSellerCommission olarak yansir.
    /// </summary>
    public decimal SelectedServiceFee { get; set; } = 0;

    /// <summary>
    /// AirSearch response'undaki brandedFareItems listesinden secilen paketin ID'si.
    /// Gonderilmezse sistem otomatik en dusuk paketi secer.
    /// </summary>
    public string? BrandedFareItemId { get; set; }

    /// <summary>
    /// RecommendationBox gidis bacaginin FlightId'si — SubOptions icin.
    /// </summary>
    public string? DepartureFlightId { get; set; }

    /// <summary>
    /// RecommendationBox donus bacaginin FlightId'si — SubOptions icin.
    /// </summary>
    public string? ReturnFlightId { get; set; }
}
