using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities;

public class FlightSegment
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public int SequenceNo { get; set; }
    public string MarketingAirline { get; set; }
    public string? OperatingAirline { get; set; }
    public string FlightNumber { get; set; }
    public string OriginCode { get; set; }
    public string DestinationCode { get; set; }
    public DateTime DepartureDate { get; set; }
    public string? DepartureTime { get; set; }
    public DateTime? ArrivalDate { get; set; }
    public string? ArrivalTime { get; set; }
    public string? BookingClass { get; set; }
    public string? Cabin { get; set; }
    public string? FareBasis { get; set; }
    public string? Baggage { get; set; }
    public string? TicketNumber { get; set; }
    public string? Equipment { get; set; }

    public Booking Booking { get; set; }
}