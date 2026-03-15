using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GBILET.Infrastructure.Entity;

public class Trip
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    [MaxLength(200)]
    public string? TripName { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; } // Planned, Active, Completed, Cancelled

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
    public virtual ICollection<TripBooking> TripBookings { get; set; } = new List<TripBooking>();
    public virtual ICollection<TripPassenger> TripPassengers { get; set; } = new List<TripPassenger>();
}
