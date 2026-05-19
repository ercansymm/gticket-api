namespace GBILET.Core.Entities.Admin;

public class AdminUser
{
    public Guid Id { get; set; }

    // Kimlik
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;

    // Parola (BCrypt hash, asla plaintext)
    public string PasswordHash { get; set; } = null!;

    // Rol
    public AdminRole Role { get; set; } = AdminRole.ReadOnly;

    // 2FA (TOTP)
    public string? TwoFactorSecret { get; set; }   // Base32 encoded secret
    public bool TwoFactorEnabled { get; set; } = false;

    // Hesap durumu
    public bool IsActive { get; set; } = true;
    public DateTime? LockedUntil { get; set; }       // Başarısız deneme kilidi için
    public int FailedLoginAttempts { get; set; } = 0;

    // Son oturum bilgisi
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public List<AdminRefreshToken> RefreshTokens { get; set; } = new();
    public List<AdminAuditLog> AuditLogs { get; set; } = new();
}