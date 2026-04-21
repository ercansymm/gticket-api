using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities;

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

    // Kart bilgileri (maskelenmiş)
    public string? CardLastFour { get; set; }
    public string? CardHolder { get; set; }

    // 3D Secure
    public bool Is3DSecure { get; set; } = false;
    public string? RedirectUrl { get; set; }

    // Ödeme sağlayıcı
    public string? ProviderTransactionId { get; set; }
    public string? ErrorMessage { get; set; }

    //  YENİ — BiletBank referansı
    public string? BiletBankPaymentId { get; set; }

    //  YENİ — Ödeme tipi (RunningAccount / CreditCard / CreditCardDirect)
    public string? PaymentType { get; set; }

    //  YENİ — Hata kategorisi (filtre için)
    // Values: "CardLimit", "ThreeDFailed", "BankRejected", "Timeout", "InvalidCard", "Other"
    public string? ErrorCode { get; set; }

    //  YENİ — Raw SOAP (debug için)
    public string? RawRequest { get; set; }
    public string? RawResponse { get; set; }

    // İade
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundAmount { get; set; }

    public Booking Booking { get; set; }
}