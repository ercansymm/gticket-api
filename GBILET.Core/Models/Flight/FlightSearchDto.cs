namespace GBILET.Core.Models.Flight;

public class FlightSearchResponseDto
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SearchId { get; set; }
    public string? SessionId { get; set; }
    public string? SessionToken { get; set; }
    public List<FlightResultDto> Flights { get; set; } = [];
    public FlightFilterOptionsDto? FilterOptions { get; set; }

    /// <summary>
    /// Geçici debug bilgisi — BiletBank XML yapısını incelemek için.
    /// Sorun çözüldükten sonra kaldırılacak.
    /// </summary>
    public object? _debug { get; set; }
}

public class FlightResultDto
{
    // Kimlik
    public string? ProductId { get; set; }
    public string? ProductItemId { get; set; }

    // Havayolu
    public string? AirlineCode { get; set; }
    public string? AirlineName { get; set; }
    public string? FlightNumber { get; set; }
    public string? BookingProvider { get; set; }

    // Güzergah
    public string? OriginCode { get; set; }
    public string? OriginName { get; set; }
    public string? DestinationCode { get; set; }
    public string? DestinationName { get; set; }

    // Zaman
    public string? DepartureDate { get; set; }
    public string? DepartureTime { get; set; }
    public string? ArrivalDate { get; set; }
    public string? ArrivalTime { get; set; }
    public int DurationHours { get; set; }
    public int DurationMinutes { get; set; }
    public string? DurationFormatted { get; set; }

    // Uçak
    public string? Equipment { get; set; }

    // Fiyat
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal TotalFare { get; set; }
    public string? Currency { get; set; }
    public string? TotalFareFormatted { get; set; }

    // Durum
    public bool IsRefundable { get; set; }
    public bool IsReservable { get; set; }
    public string? RefundableText { get; set; }

    // Sınıf
    public string? FareType { get; set; }
    public string? BookingClass { get; set; }
    public string? BookingClassName { get; set; }
    public string? CabinClass { get; set; }
    public string? CabinClassName { get; set; }

    // Kapasite
    public int AvailableSeats { get; set; }
    public string? AvailableSeatsText { get; set; }

    // Aktarma
    public int StopCount { get; set; }
    public bool IsDirect { get; set; }
    public string? StopText { get; set; }

    // Segmentler
    public List<FlightSegmentDto> Segments { get; set; } = [];

    // Komisyon
    public decimal CustomerCommissionMin { get; set; }
    public decimal CustomerCommissionMax { get; set; }
    public decimal CustomerCommissionValue { get; set; }

    /// <summary>
    /// Varsayilan (en dusuk fiyatli) branded fare paket ID'si.
    /// Frontend allocate'e bunu gonderir, kullanici degistirmedikce.
    /// </summary>
    public string? DefaultBrandedFareItemId { get; set; }

    // Bagaj ham veri
    public List<FreeBaggageAllowance> FreeBaggageAllowances { get; set; } = [];

    // Paket secenekleri (EcoFly, ExtraFly, PrimeFly vb.) — tum paketler
    public List<BrandedFareOptionDto> FarePackages { get; set; } = [];

    // Bagaj bilgisi özeti
    public BaggageInfoDto? BaggageInfo { get; set; }
}

public class FlightSegmentDto
{
    public int SequenceNo { get; set; }
    public string? OriginCode { get; set; }
    public string? OriginName { get; set; }
    public string? DestinationCode { get; set; }
    public string? DestinationName { get; set; }
    public string? DepartureDate { get; set; }
    public string? DepartureTime { get; set; }
    public string? ArrivalDate { get; set; }
    public string? ArrivalTime { get; set; }
    public int DurationHours { get; set; }
    public int DurationMinutes { get; set; }
    public string? DurationFormatted { get; set; }
    public string? AirlineCode { get; set; }
    public string? AirlineName { get; set; }
    public string? FlightNumber { get; set; }
    public string? Equipment { get; set; }
    public string? BookingClass { get; set; }
    public string? BookingClassName { get; set; }
    public string? FareType { get; set; }
    public string? FareTypeName { get; set; }

    // Aktarma bekleme süresi (ilk segment hariç)
    public int? LayoverMinutes { get; set; }
    public string? LayoverFormatted { get; set; }
}

public class FlightFilterOptionsDto
{
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public List<AirlineFilterItem> Airlines { get; set; } = [];
    public bool HasDirectFlights { get; set; }
    public bool HasRefundableFlights { get; set; }
    public string? EarliestDeparture { get; set; }
    public string? LatestDeparture { get; set; }
    public List<string> CabinClasses { get; set; } = [];
    public List<string> FarePackages { get; set; } = [];
}

public class AirlineFilterItem
{
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public class BrandedFareOptionDto
{
    public string? BrandedFareItemId { get; set; }
    public string? BrandCode { get; set; }
    public string? BrandName { get; set; }
    public decimal TotalFare { get; set; }
    public decimal TotalTaxes { get; set; }
    public string? Currency { get; set; }
    public string? TotalFareFormatted { get; set; }
    public string? CabinClass { get; set; }
    public string? BookingClass { get; set; }

    /// <summary>
    /// Bu paketin baz fiyata gore fark tutari.
    /// Negatif ise baz fiyattan ucuz, pozitif ise pahali.
    /// </summary>
    public decimal PriceDifference { get; set; }
    public string? PriceDifferenceFormatted { get; set; }

    /// <summary>
    /// Yolcu bazli fiyat dagilimi (ADT, CHD, INF)
    /// </summary>
    public List<PassengerFareBreakdownDto> PassengerFares { get; set; } = [];

    /// <summary>
    /// Paket kurallari (bagaj, iade, degisiklik vb.)
    /// </summary>
    public List<BrandedRuleDto> Rules { get; set; } = [];

    /// <summary>
    /// Bu paket en dusuk fiyatli mi? (varsayilan secim icin)
    /// </summary>
    public bool IsDefault { get; set; }
}

public class PassengerFareBreakdownDto
{
    public string? PassengerType { get; set; }
    public int PassengerCount { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal TotalFare { get; set; }
    public string? Currency { get; set; }
}

public class BrandedRuleDto
{
    public string? Description { get; set; }
    public bool IsIncluded { get; set; }
    public bool IsChargeable { get; set; }
    public string? ServiceGroup { get; set; }
    public string? Application { get; set; }
}

public class BaggageInfoDto
{
    public string? Allowance { get; set; }
    public string? Unit { get; set; }
    public string? DisplayText { get; set; }
    public string? Category { get; set; }
}
