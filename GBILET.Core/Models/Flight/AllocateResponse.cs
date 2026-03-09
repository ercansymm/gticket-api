namespace GBILET.Core.Models.Flight;

public class AllocateResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Shopping dosya ID'si (sonraki adýmlarda kullanýlýr)
    /// </summary>
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Son allocate edilen ürün ID'leri
    /// </summary>
    public List<string> LastAllocatedProductIds { get; set; } = [];

    /// <summary>
    /// Fiyat deðiþti mi?
    /// </summary>
    public bool IsPriceChanged { get; set; }

    /// <summary>
    /// Uçuþ bilgisi deðiþti mi?
    /// </summary>
    public bool? IsFlightInfoChanged { get; set; }

    /// <summary>
    /// Dosya para birimi
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Allocate sonrasý dönen AirBooking bilgileri
    /// </summary>
    public List<AllocateAirBooking> AirBookings { get; set; } = [];

    /// <summary>
    /// Fiyat özeti
    /// </summary>
    public AllocatePriceSummary? PriceSummary { get; set; }

    /// <summary>
    /// Oturum bilgileri (stateless yanýttan dönen SessionId/Token, sonraki çaðrýlarda kullanýlabilir)
    /// </summary>
    public string? SessionId { get; set; }
    public string? SessionToken { get; set; }

    /// <summary>
    /// Debug: Ham SOAP yanýtý (geçici - production'da kaldýrýlacak)
    /// </summary>
    public string? RawSoapResponse { get; set; }

    /// <summary>
    /// Debug: Parse aþamasý bilgisi (geçici)
    /// </summary>
    public string? DebugInfo { get; set; }
}

public class AllocateAirBooking
{
    public string? ProductId { get; set; }
    public string? PNR { get; set; }
    public string? BookingProvider { get; set; }
    public string? Status { get; set; }
    public string? Currency { get; set; }
    public decimal TotalFare { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal ServiceFee { get; set; }
    public List<AllocateSegment> Segments { get; set; } = [];
}

public class AllocateSegment
{
    public string? OriginCode { get; set; }
    public string? DestinationCode { get; set; }
    public string? DepartureDay { get; set; }
    public string? DepartureTime { get; set; }
    public string? ArrivalDay { get; set; }
    public string? ArrivalTime { get; set; }
    public string? MarketingAirline { get; set; }
    public string? OperatingAirline { get; set; }
    public string? FlightNumber { get; set; }
    public string? BookingClass { get; set; }
}

public class AllocatePriceSummary
{
    public decimal GrandTotal { get; set; }
    public decimal TotalBaseFare { get; set; }
    public decimal TotalTaxes { get; set; }
    public decimal TotalServiceFee { get; set; }
    public string? Currency { get; set; }
}
