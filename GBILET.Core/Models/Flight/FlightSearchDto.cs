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

    // Sýnýf
    public string? FareType { get; set; }
    public string? BookingClass { get; set; }
    public string? BookingClassName { get; set; }

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

    // Branded Fare (ileride dolabilir)
    public List<BrandedFareItem> BrandedFareItems { get; set; } = [];
    public List<FreeBaggageAllowance> FreeBaggageAllowances { get; set; } = [];
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
}

public class AirlineFilterItem
{
    public string? Code { get; set; }
    public string? Name { get; set; }
}
