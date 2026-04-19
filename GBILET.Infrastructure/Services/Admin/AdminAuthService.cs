using System.Security.Cryptography;
using System.Text;
using GBILET.Core.DTOs.Admin;
using GBILET.Core.Entities.Admin;
using GBILET.Core.Service.Admin;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GBILET.Infrastructure.Services.Admin;

public class AdminAuthService : IAdminAuthService
{
    private readonly GTicketDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ITokenService _tokens;
    private readonly ITotpService _totp;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AdminAuthService> _logger;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;
    private const int TwoFactorTokenLifetimeMinutes = 5;

    // 2FA temporary token'larını memory'de tutuyoruz (kısa ömürlü)
    // Production için Redis ya da DB tablosu daha iyi olur
    private static readonly Dictionary<string, (Guid UserId, DateTime ExpiresAt)> _twoFactorPending = new();
    private static readonly object _pendingLock = new();

    public AdminAuthService(
        GTicketDbContext db,
        IPasswordService passwords,
        ITokenService tokens,
        ITotpService totp,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AdminAuthService> logger)
    {
        _db = db;
        _passwords = passwords;
        _tokens = tokens;
        _totp = totp;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<(AdminLoginResponse Response, AdminTokens? Tokens)> LoginAsync(
        AdminLoginRequest request, string? ipAddress, string? userAgent)
    {
        var user = await _db.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        // Tip 1 — kullanıcı yok VEYA pasif: aynı hata mesajını döner
        // (timing-safe değil ama best effort, kullanıcı keşfini zorlaştırır)
        if (user == null || !user.IsActive)
        {
            await LogAuditAsync(null, request.Username, "LoginFailed", false,
                "Invalid credentials or inactive user", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Geçersiz kullanıcı adı veya parola.");
        }

        // Tip 2 — kilitli mi
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            await LogAuditAsync(user.Id, user.Username, "LoginBlocked", false,
                $"Account locked, {remaining} minutes remaining", ipAddress, userAgent);
            throw new UnauthorizedAccessException(
                $"Hesap geçici olarak kilitli. {remaining} dakika sonra tekrar deneyin.");
        }

        // Tip 3 — parola yanlış
        if (!_passwords.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                _logger.LogWarning("Admin user {Username} locked for {Minutes} min after {Attempts} failed attempts",
                    user.Username, LockoutMinutes, user.FailedLoginAttempts);
            }
            await _db.SaveChangesAsync();

            await LogAuditAsync(user.Id, user.Username, "LoginFailed", false,
                $"Wrong password (attempt {user.FailedLoginAttempts})", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Geçersiz kullanıcı adı veya parola.");
        }

        // Başarılı parola — counter sıfırla, kilit aç
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        // 2FA aktif mi?
        if (user.TwoFactorEnabled)
        {
            // Temporary token üret (5dk), asıl token verme
            var tempToken = GenerateSecureToken();
            lock (_pendingLock)
            {
                CleanupExpiredPendingTokens();
                _twoFactorPending[tempToken] = (user.Id, DateTime.UtcNow.AddMinutes(TwoFactorTokenLifetimeMinutes));
            }

            await _db.SaveChangesAsync();
            await LogAuditAsync(user.Id, user.Username, "LoginPasswordOk2FaPending", true,
                null, ipAddress, userAgent);

            return (new AdminLoginResponse(
                RequiresTwoFactor: true,
                TwoFactorToken: tempToken,
                User: null,
                AccessTokenExpiresAt: null
            ), null);
        }

        // 2FA yok — direkt asıl token üret
        return await IssueTokensAsync(user, ipAddress, userAgent, "Login");
    }

    public async Task<(AdminLoginResponse Response, AdminTokens? Tokens)> VerifyTwoFactorAsync(
        AdminVerify2FaRequest request, string? ipAddress, string? userAgent)
    {
        Guid userId;
        lock (_pendingLock)
        {
            CleanupExpiredPendingTokens();
            if (!_twoFactorPending.TryGetValue(request.TwoFactorToken, out var entry))
                throw new UnauthorizedAccessException("2FA oturumu süresi doldu, lütfen tekrar giriş yapın.");
            userId = entry.UserId;
        }

        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsActive || !user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            await LogAuditAsync(userId, null, "Verify2FaFailed", false, "Invalid user state", ipAddress, userAgent);
            throw new UnauthorizedAccessException("2FA doğrulaması başarısız.");
        }

        if (!_totp.VerifyCode(user.TwoFactorSecret, request.Code))
        {
            await LogAuditAsync(user.Id, user.Username, "Verify2FaFailed", false, "Wrong code", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Doğrulama kodu hatalı.");
        }

        // Temp token'ı temizle (tek kullanımlık)
        lock (_pendingLock)
        {
            _twoFactorPending.Remove(request.TwoFactorToken);
        }

        return await IssueTokensAsync(user, ipAddress, userAgent, "Login2FaSuccess");
    }

    public async Task<AdminTokens?> RefreshAsync(string refreshToken, string? ipAddress, string? userAgent)
    {
        var hash = _tokens.HashToken(refreshToken);
        var stored = await _db.AdminRefreshTokens
            .Include(t => t.AdminUser)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored == null)
        {
            // Bilinmeyen token — silent fail
            return null;
        }

        // Token revoke edilmiş ama kullanılıyor → güvenlik ihlali, tüm token'ları iptal et
        if (stored.RevokedAt.HasValue)
        {
            _logger.LogWarning("Revoked refresh token reuse detected for user {UserId} from {Ip}",
                stored.AdminUserId, ipAddress);
            await RevokeAllForUserAsync(stored.AdminUserId, ipAddress, "Detected reuse of revoked token");
            await LogAuditAsync(stored.AdminUserId, stored.AdminUser.Username, "RefreshTokenReuseDetected",
                false, "All sessions invalidated", ipAddress, userAgent);
            return null;
        }

        // Süresi geçmiş
        if (stored.IsExpired)
        {
            return null;
        }

        // Kullanıcı pasifleştirilmiş olabilir
        if (!stored.AdminUser.IsActive)
        {
            return null;
        }

        // Rotation: eskiyi revoke et, yenisini ver
        var newRefreshToken = _tokens.GenerateRefreshToken();
        var newRefreshExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        var newStored = new AdminRefreshToken
        {
            Id = Guid.NewGuid(),
            AdminUserId = stored.AdminUserId,
            TokenHash = _tokens.HashToken(newRefreshToken),
            ExpiresAt = newRefreshExpiresAt,
            CreatedByIp = ipAddress,
            UserAgent = userAgent
        };
        _db.AdminRefreshTokens.Add(newStored);

        stored.RevokedAt = DateTime.UtcNow;
        stored.RevokedByIp = ipAddress;
        stored.RevokedReason = "rotated";
        stored.ReplacedByTokenId = newStored.Id;

        var (accessToken, accessExpiresAt) = _tokens.GenerateAccessToken(stored.AdminUser);

        await _db.SaveChangesAsync();
        await LogAuditAsync(stored.AdminUserId, stored.AdminUser.Username, "TokenRefreshed",
            true, null, ipAddress, userAgent);

        return new AdminTokens(accessToken, accessExpiresAt, newRefreshToken, newRefreshExpiresAt);
    }

    public async Task LogoutAsync(string refreshToken, string? ipAddress)
    {
        var hash = _tokens.HashToken(refreshToken);
        var stored = await _db.AdminRefreshTokens
            .Include(t => t.AdminUser)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored == null || stored.RevokedAt.HasValue)
            return; // Sessizce başarılı sayılır

        stored.RevokedAt = DateTime.UtcNow;
        stored.RevokedByIp = ipAddress;
        stored.RevokedReason = "logout";

        await _db.SaveChangesAsync();
        await LogAuditAsync(stored.AdminUserId, stored.AdminUser?.Username, "Logout", true, null, ipAddress, null);
    }

    public async Task<AdminSetup2FaResponse> InitiateTwoFactorSetupAsync(Guid adminUserId)
    {
        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == adminUserId);
        if (user == null) throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

        var secret = _totp.GenerateSecret();
        var uri = _totp.BuildOtpAuthUri(secret, user.Username);
        var qr = _totp.GenerateQrCodeBase64(uri);

        // Secret'ı şimdilik kaydet ama enabled=false bırak
        // ConfirmTwoFactorSetup'ta enabled=true olacak
        user.TwoFactorSecret = secret;
        user.TwoFactorEnabled = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AdminSetup2FaResponse(secret, qr);
    }

    public async Task<bool> ConfirmTwoFactorSetupAsync(Guid adminUserId, string code)
    {
        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == adminUserId);
        if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret))
            throw new UnauthorizedAccessException("Önce 2FA setup başlatılmalı.");

        if (!_totp.VerifyCode(user.TwoFactorSecret, code))
            return false;

        user.TwoFactorEnabled = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LogAuditAsync(user.Id, user.Username, "TwoFactorEnabled", true, null, null, null);
        return true;
    }

    // ============= HELPERS =============

    private async Task<(AdminLoginResponse, AdminTokens)> IssueTokensAsync(
        AdminUser user, string? ipAddress, string? userAgent, string auditAction)
    {
        var (accessToken, accessExpiresAt) = _tokens.GenerateAccessToken(user);
        var refreshToken = _tokens.GenerateRefreshToken();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        _db.AdminRefreshTokens.Add(new AdminRefreshToken
        {
            Id = Guid.NewGuid(),
            AdminUserId = user.Id,
            TokenHash = _tokens.HashToken(refreshToken),
            ExpiresAt = refreshExpiresAt,
            CreatedByIp = ipAddress,
            UserAgent = userAgent
        });

        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = ipAddress;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await LogAuditAsync(user.Id, user.Username, auditAction, true, null, ipAddress, userAgent);

        return (new AdminLoginResponse(
            RequiresTwoFactor: false,
            TwoFactorToken: null,
            User: user.ToDto(),
            AccessTokenExpiresAt: accessExpiresAt
        ), new AdminTokens(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt));
    }

    private async Task RevokeAllForUserAsync(Guid userId, string? ipAddress, string reason)
    {
        var activeTokens = await _db.AdminRefreshTokens
            .Where(t => t.AdminUserId == userId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var t in activeTokens)
        {
            t.RevokedAt = DateTime.UtcNow;
            t.RevokedByIp = ipAddress;
            t.RevokedReason = reason;
        }
        await _db.SaveChangesAsync();
    }

    private async Task LogAuditAsync(
        Guid? userId, string? username, string action, bool success,
        string? errorMessage, string? ipAddress, string? userAgent)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = userId,
            Username = username,
            Action = action,
            Success = success,
            ErrorMessage = errorMessage,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });
        await _db.SaveChangesAsync();
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static void CleanupExpiredPendingTokens()
    {
        var now = DateTime.UtcNow;
        var expired = _twoFactorPending.Where(kvp => kvp.Value.ExpiresAt < now).Select(kvp => kvp.Key).ToList();
        foreach (var key in expired) _twoFactorPending.Remove(key);
    }
}
