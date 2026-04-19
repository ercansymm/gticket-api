using GBILET.Core.Entities.Admin;

namespace GBILET.Core.DTOs.Admin;

// ===== REQUESTS =====

public record AdminLoginRequest(string Username, string Password);

public record AdminVerify2FaRequest(string TwoFactorToken, string Code);

public record AdminSetup2FaRequest(string Code);

// ===== RESPONSES =====

public record AdminLoginResponse(
    bool RequiresTwoFactor,
    string? TwoFactorToken,         // 5dk geçerli, sadece 2FA verify için
    AdminUserDto? User,             // 2FA gerekmiyorsa direkt user dönüyor
    DateTime? AccessTokenExpiresAt
);

public record AdminUserDto(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string Role,
    bool TwoFactorEnabled,
    DateTime? LastLoginAt
);

public record AdminSetup2FaResponse(
    string Secret,                  // Manuel girmek isteyenler için
    string QrCodeBase64             // <img src="..." />
);

public record AdminTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt
);

// AdminUser'dan AdminUserDto'ya dönüşüm helper'ı
public static class AdminUserMapper
{
    public static AdminUserDto ToDto(this AdminUser u) => new(
        u.Id, u.Username, u.Email, u.FullName,
        u.Role.ToString(), u.TwoFactorEnabled, u.LastLoginAt
    );
}
