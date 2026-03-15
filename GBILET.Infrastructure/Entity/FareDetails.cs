using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GBILET.Infrastructure.Entity;

public class FareDetails
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }

    [MaxLength(5)]
    public string? PaxCode { get; set; } // ADT, CHD, INF

    [MaxLength(5)]
    public string? Currency { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseFare { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Taxes { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ServiceFee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SystemServiceFee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalFare { get; set; }

    // Profit Tracking (Kâr takibi)
    [Column(TypeName = "decimal(18,2)")]
    public decimal BiletBankCost { get; set; } // BiletBank'ın bize maliyeti

    [Column(TypeName = "decimal(18,2)")]
    public decimal OurPrice { get; set; } // Bizim müşteriye sattığımız fiyat

    [Column(TypeName = "decimal(18,2)")]
    public decimal Profit { get; set; } // OurPrice - BiletBankCost

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Commission { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CommissionMin { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CommissionMax { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}
