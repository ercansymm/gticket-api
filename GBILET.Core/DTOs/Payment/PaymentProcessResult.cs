using GBILET.Core.Models.Flight;

namespace GBILET.Core.DTOs.Payment;

/// <summary>
/// PaymentService isleminin sonucu. Controller bunu Ok(...) ile dogrudan frontend'e doner.
/// Alan isimleri MakePaymentResponse ile birebir uyumlu — frontend mevcut response sozlesmesini bozmadan calisir.
/// </summary>
public class PaymentProcessResult
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    public bool IsPaymentSuccessful { get; set; }
    public string? Status { get; set; }

    public string? ShoppingFileId { get; set; }
    public decimal RemainingSum { get; set; }
    public string? Currency { get; set; }
    public string? PaymentReferenceId { get; set; }

    public string? PNR { get; set; }
    public string? BookingStatus { get; set; }
    public decimal? RunningAccountBalance { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>3D Secure HTML formu — gerekirse iframe icinde gosterilir.</summary>
    public string? ThreeDSecureUrl { get; set; }
    public bool Is3DSecureRequired { get; set; }
    public string? ThreeDSecureHtml { get; set; }

    public List<PaymentInstallmentOption> InstallmentOptions { get; set; } = new();

    public bool AutoFinalized { get; set; }
    public string? FinalizeStatus { get; set; }
    public string? InternalPnr { get; set; }
    public List<TicketInfo> Tickets { get; set; } = new();

    /// <summary>
    /// Yeni — DB'deki Payment satirinin Id'si. Audit/log icin frontend ile paylasilir.
    /// </summary>
    public Guid? PaymentId { get; set; }
}
