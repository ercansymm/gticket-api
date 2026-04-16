namespace GBILET.Core.Models.Ticket;

public class TicketPdfDataDto
{
    public string PassengerName { get; set; } = string.Empty;
    public string Pnr { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }

    public string? TcNo { get; set; }
    public string? PassportNo { get; set; }
    public string? PassportCountry { get; set; }
    public bool IsInternational { get; set; }

    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal TotalFare { get; set; }
    public string Currency { get; set; } = "TRY";

    /// <summary>
    /// Kullanicinin sectigi gosterim para birimi. null veya "TRY" ise donusum yapilmaz.
    /// </summary>
    public string? DisplayCurrency { get; set; }

    /// <summary>
    /// Gosterim para birimindeki toplam tutar.
    /// </summary>
    public decimal? DisplayTotalFare { get; set; }

    /// <summary>
    /// Gosterim para birimindeki baz ucret.
    /// </summary>
    public decimal? DisplayBaseFare { get; set; }

    /// <summary>
    /// Gosterim para birimindeki vergi tutari.
    /// </summary>
    public decimal? DisplayTaxes { get; set; }

    /// <summary>
    /// Doviz kuru (1 DisplayCurrency = X TRY).
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    public List<TicketFareItemDto> FareItems { get; set; } = [];
    public List<TicketFlightDto> Flights { get; set; } = [];

    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
}

public class TicketFareItemDto
{
    public string Route { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
}

public class TicketFlightDto
{
    public string AirlineName { get; set; } = string.Empty;
    public string FlightCode { get; set; } = string.Empty;
    public string BookingClass { get; set; } = string.Empty;
    public string? FareBasisName { get; set; }

    public string OriginCity { get; set; } = string.Empty;
    public string OriginAirport { get; set; } = string.Empty;
    public string OriginCode { get; set; } = string.Empty;
    public string DepartureDate { get; set; } = string.Empty;
    public string DepartureTime { get; set; } = string.Empty;

    public string DestinationCity { get; set; } = string.Empty;
    public string DestinationAirport { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;
    public string ArrivalDate { get; set; } = string.Empty;
    public string ArrivalTime { get; set; } = string.Empty;

    public string BaggageAllowance { get; set; } = string.Empty;
    public string AirlineCode { get; set; } = string.Empty;
}
