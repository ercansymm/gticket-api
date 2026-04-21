using GBILET.Core.DTOs.Admin;
using GBILET.Core.Entities.Admin;
using GBILET.Core.Service.Admin;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GBILET.Infrastructure.Services.Admin;

public class AdminUserManagementService : IAdminUserManagementService
{
    private readonly GTicketDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ILogger<AdminUserManagementService> _logger;

    public AdminUserManagementService(
        GTicketDbContext db,
        IPasswordService passwords,
        ILogger<AdminUserManagementService> logger)
    {
        _db = db;
        _passwords = passwords;
        _logger = logger;
    }

    // ============================================================
    // LIST
    // ============================================================
    public async Task<List<AdminUserListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.AdminUsers
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserListItemDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                FullName = u.FullName,
                Role = u.Role,
                IsActive = u.IsActive,
                TwoFactorEnabled = u.TwoFactorEnabled,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(ct);
    }

    // ============================================================
    // CREATE
    // ============================================================
    public async Task<AdminUserListItemDto> CreateAsync(
        CreateAdminUserRequest request,
        Guid createdByUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLower();

        // Duplicate kontrolü (case-insensitive email)
        var exists = await _db.AdminUsers
            .AnyAsync(u =>
                u.Username.ToLower() == username.ToLower() ||
                u.Email.ToLower() == email, ct);

        if (exists)
        {
            await LogAuditAsync(
                createdByUserId, null, "AdminCreateFailed",
                success: false,
                errorMessage: $"Username or email already exists: {username} / {email}",
                targetEntity: "AdminUser", targetId: null,
                ipAddress: ipAddress, userAgent: userAgent);

            throw new InvalidOperationException("Bu kullanıcı adı veya email zaten kullanılıyor.");
        }

        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = _passwords.HashPassword(request.Password),
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.AdminUsers.Add(user);
        await _db.SaveChangesAsync(ct);

        await LogAuditAsync(
            createdByUserId, null, "AdminCreated",
            success: true, errorMessage: null,
            targetEntity: "AdminUser", targetId: user.Id.ToString(),
            ipAddress: ipAddress, userAgent: userAgent,
            details: $"Role={user.Role}, Username={user.Username}");

        _logger.LogInformation("Admin user {Username} created by {CreatedBy}", user.Username, createdByUserId);

        return ToDto(user);
    }


// ============================================================
// GET BY ID
// ============================================================
public async Task<AdminUserDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    return await _db.AdminUsers
        .AsNoTracking()
        .Where(u => u.Id == id)
        .Select(u => new AdminUserDetailDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            FullName = u.FullName,
            Role = u.Role,
            IsActive = u.IsActive,
            TwoFactorEnabled = u.TwoFactorEnabled,
            LastLoginAt = u.LastLoginAt,
            FailedLoginAttempts = u.FailedLoginAttempts,
            LockedUntil = u.LockedUntil,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        })
        .FirstOrDefaultAsync(ct);
}












    // ============================================================
    // UPDATE STATUS (Activate / Deactivate)
    // ============================================================
    public async Task<AdminUserListItemDto> UpdateStatusAsync(
        Guid targetUserId,
        bool isActive,
        Guid performedByUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        // Kural 1: Kendini deaktive edemezsin
        if (targetUserId == performedByUserId && !isActive)
        {
            throw new InvalidOperationException("Kendi hesabınızı deaktive edemezsiniz.");
        }

        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == targetUserId, ct)
            ?? throw new KeyNotFoundException("Admin kullanıcı bulunamadı.");

        // Kural 2: Son aktif SuperAdmin'i deaktive edemezsin
        if (!isActive && user.Role == AdminRole.SuperAdmin && user.IsActive)
        {
            var activeSuperAdminCount = await _db.AdminUsers
                .CountAsync(u => u.Role == AdminRole.SuperAdmin && u.IsActive, ct);

            if (activeSuperAdminCount <= 1)
            {
                throw new InvalidOperationException(
                    "Sistemdeki son aktif SuperAdmin'i deaktive edemezsiniz.");
            }
        }

        // Idempotent: zaten aynı durumdaysa logla ve dön
        if (user.IsActive == isActive)
        {
            return ToDto(user);
        }

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var action = isActive ? "AdminActivated" : "AdminDeactivated";
        await LogAuditAsync(
            performedByUserId, null, action,
            success: true, errorMessage: null,
            targetEntity: "AdminUser", targetId: user.Id.ToString(),
            ipAddress: ipAddress, userAgent: userAgent,
            details: $"Username={user.Username}");

        _logger.LogInformation("Admin {Username} status set to IsActive={IsActive} by {PerformedBy}",
            user.Username, isActive, performedByUserId);

        return ToDto(user);
    }

    // ============================================================
    // RESET PASSWORD
    // ============================================================
    public async Task ResetPasswordAsync(
        Guid targetUserId,
        string newPassword,
        Guid performedByUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == targetUserId, ct)
            ?? throw new KeyNotFoundException("Admin kullanıcı bulunamadı.");

        user.PasswordHash = _passwords.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        await _db.SaveChangesAsync(ct);

        await LogAuditAsync(
            performedByUserId, null, "AdminPasswordReset",
            success: true, errorMessage: null,
            targetEntity: "AdminUser", targetId: user.Id.ToString(),
            ipAddress: ipAddress, userAgent: userAgent,
            details: $"Username={user.Username}");

        _logger.LogInformation("Password reset for admin {Username} by {PerformedBy}",
            user.Username, performedByUserId);
    }

    // ============================================================
    // Helpers
    // ============================================================
    private static AdminUserListItemDto ToDto(AdminUser u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        FullName = u.FullName,
        Role = u.Role,
        IsActive = u.IsActive,
        TwoFactorEnabled = u.TwoFactorEnabled,
        LastLoginAt = u.LastLoginAt,
        CreatedAt = u.CreatedAt
    };

    private async Task LogAuditAsync(
        Guid? userId, string? username, string action, bool success,
        string? errorMessage,
        string? targetEntity, string? targetId,
        string? ipAddress, string? userAgent,
        string? details = null)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = userId,
            Username = username,
            Action = action,
            TargetEntity = targetEntity,
            TargetId = targetId,
            Details = details,
            Success = success,
            ErrorMessage = errorMessage,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });
        await _db.SaveChangesAsync();
    }
}