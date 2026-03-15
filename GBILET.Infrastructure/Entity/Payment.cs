using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GBILET.Infrastructure.Entity;

public class Payment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; } // CreditCard, BankTransfer, Cash

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(5)]
    public string? Currency { get; set; } // TRY, USD, EUR

    [MaxLength(50)]
    public string? Status { get; set; } // Pending, Completed, Failed, Refunded

    [MaxLength(100)]
    public string? TransactionId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}
