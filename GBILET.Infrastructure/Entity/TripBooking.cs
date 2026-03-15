using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GBILET.Infrastructure.Entity;

public class TripBooking
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TripId { get; set; }
    public Guid BookingId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey("TripId")]
    public virtual Trip Trip { get; set; } = null!;

    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}
