namespace GBILET.Infrastructure.Entity;

public class Payment
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? CardHolderName { get; set; }
    public string? MaskedCardNumber { get; set; }
    public int InstallmentCount { get; set; } = 1;
    public string Status { get; set; } = "Pending";
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    // Kart bilgileri (maskelenmiþ)
    public string? CardLastFour { get; set; }
    public string? CardHolder { get; set; }

    // 3D Secure
    public bool Is3DSecure { get; set; } = false;
    public string? RedirectUrl { get; set; }

    // Ödeme saðlayýcý
    public string? ProviderTransactionId { get; set; }
    public string? ErrorMessage { get; set; }

    // Ýade
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundAmount { get; set; }

    public Booking Booking { get; set; }
}
