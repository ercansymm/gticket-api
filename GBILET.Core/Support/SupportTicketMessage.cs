namespace GBILET.Core.Entities.Support;

public class SupportTicketMessage
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    // Gönderen
    public SupportMessageSenderType SenderType { get; set; }
    public Guid SenderId { get; set; }  // Customer ise User.Id, Admin ise AdminUser.Id

    // Görünen ad (snapshot — User/AdminUser silinse bile görünür)
    public string SenderDisplayName { get; set; } = null!;

    // İçerik
    public string Body { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SupportTicket? Ticket { get; set; }
}