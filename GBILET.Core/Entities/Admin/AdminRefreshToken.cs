namespace GBILET.Core.Entities.Admin;

public class AdminRefreshToken
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }

    // Token hash (SHA-256, raw token asla DB'de tutulmaz)
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    // Revocation / rotation
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? RevokedReason { get; set; }            // "logout", "rotated", "compromised"
    public Guid? ReplacedByTokenId { get; set; }          // Token rotation chain

    // Computed
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation
    public AdminUser AdminUser { get; set; } = null!;
}