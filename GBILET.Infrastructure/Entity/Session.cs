namespace GBILET.Infrastructure.Entity;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? BiletBankSessionId { get; set; }
    public string? BiletBankSessionToken { get; set; }
    public string? ShoppingFileId { get; set; }
    public Guid? UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
}
