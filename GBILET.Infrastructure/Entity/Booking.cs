using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GBILET.Infrastructure.Entity;

public class Booking
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; } // Nullable - anonim rezervasyonlar için

    [MaxLength(10)]
    public string? PNR { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; } // Allocated, Reserved, Ticketed, Cancelled

    public Guid? ShoppingFileId { get; set; }
    public Guid? ProductId { get; set; }

    [MaxLength(10)]
    public string? FlightType { get; set; } // OW, RT

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalFare { get; set; }

    [MaxLength(5)]
    public string? Currency { get; set; } // TRY, USD, EUR

    [MaxLength(500)]
    public string? ContactEmail { get; set; }

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
    public virtual ICollection<Passenger> Passengers { get; set; } = new List<Passenger>();
    public virtual ICollection<FlightSegment> FlightSegments { get; set; } = new List<FlightSegment>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual FareDetails? FareDetails { get; set; }
    public virtual BillingInfo? BillingInfo { get; set; }
    public virtual ICollection<BookingLog> BookingLogs { get; set; } = new List<BookingLog>();
}
