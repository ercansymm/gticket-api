namespace GBILET.Core.Models.Ticket;

public class TicketPdfDataDto
{
    public string PassengerName { get; set; } = string.Empty;
    public string Pnr { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public string? PassportOrTcNo { get; set; }

    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal TotalFare { get; set; }
    public string Currency { get; set; } = "TRY";

    public List<TicketFlightDto> Flights { get; set; } = [];

    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
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
}
