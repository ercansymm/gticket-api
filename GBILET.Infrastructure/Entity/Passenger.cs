using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GBILET.Infrastructure.Entity;

public class Passenger
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(5)]
    public string? Gender { get; set; } // M, F

    public DateTime? BirthDate { get; set; }

    [MaxLength(5)]
    public string? PaxType { get; set; } // ADT, CHD, INF

    public int SequenceNo { get; set; }

    [MaxLength(11)]
    public string? CitizenNo { get; set; } // TC Kimlik

    [MaxLength(5)]
    public string? Nationality { get; set; } // TR, US, DE

    [MaxLength(20)]
    public string? PassportNo { get; set; }

    [MaxLength(5)]
    public string? PassportCountry { get; set; }

    public DateTime? PassportValidDate { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    public bool IsContact { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}
