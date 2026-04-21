using GBILET.Core.DTOs.Admin;

namespace GBILET.Core.Service.Admin;

public interface IAdminUserManagementService
{
    Task<List<AdminUserListItemDto>> GetAllAsync(CancellationToken ct = default);

    Task<AdminUserDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);  // ← YENİ

    Task<AdminUserListItemDto> CreateAsync(
        CreateAdminUserRequest request,
        Guid createdByUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    Task<AdminUserListItemDto> UpdateStatusAsync(
        Guid targetUserId,
        bool isActive,
        Guid performedByUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    Task ResetPasswordAsync(
        Guid targetUserId,
        string newPassword,
        Guid performedByUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);
}