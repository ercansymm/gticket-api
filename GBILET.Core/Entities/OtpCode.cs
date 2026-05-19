namespace GBILET.Core.Entities;

public class OtpCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Phone { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int Purpose { get; set; } // 0 = PhoneVerification, 1 = PasswordChange
    public int AttemptCount { get; set; } = 0;
    public bool IsUsed { get; set; } = false;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
