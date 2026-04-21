namespace GBILET.Core.DTOs.Admin;

// ===== Payments =====

public class AdminPaymentListItem
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }

    // Booking referansları (arama / görüntüleme)
    public string? Pnr { get; set; }
    public string? InternalPnr { get; set; }
    public string Route { get; set; } = "";

    // Müşteri bilgisi (booking'in ilk yolcusu)
    public string CustomerName { get; set; } = "";

    // Ödeme detay
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? PaymentType { get; set; } // RunningAccount / CreditCard / CreditCardDirect
    public string? MaskedCardNumber { get; set; }
    public int InstallmentCount { get; set; } = 1;

    // Durum: Success / Failed / Pending3D / Pending
    public string Status { get; set; } = "";
    public string StatusCategory { get; set; } = ""; // success | pending | failed

    public bool Is3DSecure { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime TransactionDate { get; set; }
}

public class AdminPaymentListResponse
{
    public List<AdminPaymentListItem> Data { get; set; } = new();
    public PaginationInfo Pagination { get; set; } = new();
}

public class AdminPaymentDetail
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }

    // Booking
    public string? Pnr { get; set; }
    public string? InternalPnr { get; set; }
    public string Route { get; set; } = "";
    public string? AirlineCode { get; set; }
    public string? FlightNumber { get; set; }
    public string BookingStatus { get; set; } = "";
    public string BookingStatusCategory { get; set; } = "";

    // Müşteri (ilk yolcu + iletişim)
    public string CustomerName { get; set; } = "";
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }

    // Ödeme
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? PaymentType { get; set; }
    public string? MaskedCardNumber { get; set; }
    public string? CardHolder { get; set; }
    public int InstallmentCount { get; set; } = 1;
    public bool Is3DSecure { get; set; }

    public string Status { get; set; } = "";
    public string StatusCategory { get; set; } = "";

    public string? ErrorCode { get; set; }
    public string? ErrorCodeLabel { get; set; }
    public string? ErrorMessage { get; set; }

    // Provider
    public string? BiletBankPaymentId { get; set; }
    public string? ProviderTransactionId { get; set; }

    public DateTime TransactionDate { get; set; }
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundAmount { get; set; }
}

public class AdminFailed3DGroup
{
    public string ErrorCode { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; }
}

public class AdminFailed3DResponse
{
    public List<AdminFailed3DGroup> Groups { get; set; } = new();
    public List<AdminPaymentListItem> Data { get; set; } = new();
    public PaginationInfo Pagination { get; set; } = new();
}
