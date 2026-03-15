using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GBILET.Infrastructure.Entity;

public class BillingInfo
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [MaxLength(20)]
    public string? TaxNumber { get; set; }

    [MaxLength(100)]
    public string? TaxOffice { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(20)]
    public string? ZipCode { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}
