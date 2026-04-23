using GBILET.Core.Entities.Support;

namespace GBILET.Core.DTOs.Support;

// ============================================================
// LIST ITEM (hem müşteri hem admin listesi için)
// ============================================================
public class SupportTicketListItemDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = null!;
    public SupportTicketType Type { get; set; }
    public string Subject { get; set; } = null!;
    public SupportTicketStatus Status { get; set; }

    // Rezervasyon bilgisi (varsa)
    public Guid? BookingId { get; set; }
    public string? BookingPnr { get; set; }

    // Müşteri bilgisi (admin listesinde gösterilir; misafir ise UserId null)
    public Guid? UserId { get; set; }
    public Guid? GuestSessionId { get; set; }
    public bool IsGuest => UserId == null;
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }

    // Son mesaj özeti
    public string? LastMessagePreview { get; set; }
    public SupportMessageSenderType? LastMessageSenderType { get; set; }
    public int MessageCount { get; set; }

    public DateTime LastActivityAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================================
// DETAIL (modal/detay sayfası için, mesajlarla birlikte)
// ============================================================
public class SupportTicketDetailDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = null!;
    public SupportTicketType Type { get; set; }
    public string Subject { get; set; } = null!;
    public SupportTicketStatus Status { get; set; }

    // Rezervasyon bilgisi
    public Guid? BookingId { get; set; }
    public string? BookingPnr { get; set; }
    public string? BookingOrigin { get; set; }
    public string? BookingDestination { get; set; }
    public string? BookingStatus { get; set; }

    // Müşteri bilgisi (misafir ise UserId null)
    public Guid? UserId { get; set; }
    public Guid? GuestSessionId { get; set; }
    public bool IsGuest => UserId == null;
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserPhone { get; set; }

    // Kapatma bilgisi
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByAdminId { get; set; }
    public string? ClosedByAdminName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }

    public List<SupportTicketMessageDto> Messages { get; set; } = new();
}

// ============================================================
// MESAJ
// ============================================================
public class SupportTicketMessageDto
{
    public Guid Id { get; set; }
    public SupportMessageSenderType SenderType { get; set; }
    public Guid? SenderId { get; set; }
    public string SenderDisplayName { get; set; } = null!;
    public string Body { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

// ============================================================
// REQUEST: Müşteri yeni talep açar
// ============================================================
public class CreateSupportTicketRequest
{
    public SupportTicketType Type { get; set; }
    public string Subject { get; set; } = null!;
    public string Message { get; set; } = null!;  // İlk mesaj (body)
    public Guid? BookingId { get; set; }          // Refund/Change için zorunlu
}

// ============================================================
// REQUEST: Mesaj ekle (hem müşteri hem admin)
// ============================================================
public class AddSupportMessageRequest
{
    public string Body { get; set; } = null!;
}

// ============================================================
// ADMIN LIST FILTER
// ============================================================
public class AdminSupportTicketFilterRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public SupportTicketStatus? Status { get; set; }
    public SupportTicketType? Type { get; set; }
    public string? Search { get; set; }  // TicketNumber, PNR, müşteri adı/email
}

// ============================================================
// PAGED RESULT
// ============================================================
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// ============================================================
// GUEST: PNR + Soyad ile arama (lookup)
// ============================================================
public class GuestSupportLookupRequest
{
    public string Pnr { get; set; } = null!;
    public string Surname { get; set; } = null!;
}

public class GuestSupportLookupResponse
{
    public string Token { get; set; } = null!;       // 30 dk geçerli access token
    public DateTime ExpiresAt { get; set; }
    public Guid BookingId { get; set; }
    public string Pnr { get; set; } = null!;
    public string PassengerDisplayName { get; set; } = null!;
    public int TicketCount { get; set; }
}

// ============================================================
// GUEST: Yeni talep oluştur
// ============================================================
public class GuestCreateSupportTicketRequest
{
    public SupportTicketType Type { get; set; }
    public string Subject { get; set; } = null!;
    public string Message { get; set; } = null!;
}