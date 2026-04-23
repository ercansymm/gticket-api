using GBILET.Core.Entities.Admin;

namespace GBILET.Core.Entities.Support;

public class SupportTicket
{
    public Guid Id { get; set; }

    // Ticket numarası (örn: "TKT-2026-00001")
    public string TicketNumber { get; set; } = null!;

    // Ticket sahibi (müşteri) — üye müşteri ise dolu, misafir ise null
    public Guid? UserId { get; set; }

    // Misafir müşteri ise dolu (Booking.GuestSessionId)
    public Guid? GuestSessionId { get; set; }

    // Opsiyonel rezervasyon bağlantısı
    public Guid? BookingId { get; set; }

    // Tip ve konu
    public SupportTicketType Type { get; set; }
    public string Subject { get; set; } = null!;

    // Durum
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    // Kapatma bilgisi
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByAdminId { get; set; }

    // Son aktivite (sıralama için)
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
    public Booking? Booking { get; set; }
    public AdminUser? ClosedByAdmin { get; set; }
    public List<SupportTicketMessage> Messages { get; set; } = new();
}