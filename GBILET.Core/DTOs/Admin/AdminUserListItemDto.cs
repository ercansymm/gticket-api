using GBILET.Core.Entities.Admin;

namespace GBILET.Core.DTOs.Admin;

public class AdminUserListItemDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public AdminRole Role { get; set; }
    public bool IsActive { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}