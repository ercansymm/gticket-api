using GBILET.Core.DTOs.Admin;

namespace GBILET.Core.Service.Admin;

public interface IAdminAuthService
{
    /// <summary>
    /// Step 1: Username + parola kontrolü.
    /// 2FA aktifse temporary token döner, asıl token vermez.
    /// 2FA aktif değilse direkt access + refresh token döner.
    /// </summary>
    Task<(AdminLoginResponse Response, AdminTokens? Tokens)> LoginAsync(
        AdminLoginRequest request, string? ipAddress, string? userAgent);

    /// <summary>
    /// Step 2 (sadece 2FA aktifse): TOTP kodu doğrulanır, asıl token verilir.
    /// </summary>
    Task<(AdminLoginResponse Response, AdminTokens? Tokens)> VerifyTwoFactorAsync(
        AdminVerify2FaRequest request, string? ipAddress, string? userAgent);

    /// <summary>
    /// Refresh token ile yeni access + refresh token üretir (rotation).
    /// Eski refresh token revoke edilir.
    /// </summary>
    Task<AdminTokens?> RefreshAsync(string refreshToken, string? ipAddress, string? userAgent);

    /// <summary>
    /// Refresh token'ı revoke eder, kullanıcı çıkış yapmış olur.
    /// </summary>
    Task LogoutAsync(string refreshToken, string? ipAddress);

    /// <summary>
    /// 2FA setup başlatır — secret üretir, QR code döner. Henüz kaydetmez.
    /// </summary>
    Task<AdminSetup2FaResponse> InitiateTwoFactorSetupAsync(Guid adminUserId);

    /// <summary>
    /// 2FA setup doğrulaması — kullanıcı QR'ı tarayıp ilk kodu girer, doğruysa aktif olur.
    /// </summary>
    Task<bool> ConfirmTwoFactorSetupAsync(Guid adminUserId, string code);
}
