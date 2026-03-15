using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GBILET.Infrastructure.Entity;

public class BookingLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }

    [MaxLength(50)]
    public string? Action { get; set; } // Search, Allocate, UpdatePassengers, Reserve, Payment, Ticket, Cancel

    [MaxLength(50)]
    public string? Status { get; set; } // Success, Failed

    public string? RequestData { get; set; } // JSON veya XML

    public string? ResponseData { get; set; } // JSON veya XML

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}
