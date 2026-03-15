using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GBILET.Infrastructure.Entity;

public class FlightSegment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }

    public int SegmentSequenceNo { get; set; }

    [MaxLength(10)]
    public string? Origin { get; set; } // SAW, IST, ADB

    [MaxLength(10)]
    public string? Destination { get; set; } // TZX, GZT

    public DateTime? DepartureDate { get; set; }

    [MaxLength(10)]
    public string? DepartureTime { get; set; } // 19:55

    public DateTime? ArrivalDate { get; set; }

    [MaxLength(10)]
    public string? ArrivalTime { get; set; } // 21:40

    [MaxLength(10)]
    public string? MarketingAirline { get; set; } // VF, TK, PC

    [MaxLength(10)]
    public string? OperatingAirline { get; set; }

    [MaxLength(20)]
    public string? FlightNumber { get; set; } // VF3330

    [MaxLength(5)]
    public string? BookingClass { get; set; } // S, Y, E

    [MaxLength(20)]
    public string? FareBasis { get; set; } // SW25

    [MaxLength(20)]
    public string? FareType { get; set; } // ECO, FLEX, PREMIUM

    [MaxLength(20)]
    public string? Equipment { get; set; } // Uçak tipi

    public int? Duration { get; set; } // Dakika cinsinden

    public int? AvailableSeats { get; set; }

    [MaxLength(10)]
    public string? Direction { get; set; } // Outbound, Inbound

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}
