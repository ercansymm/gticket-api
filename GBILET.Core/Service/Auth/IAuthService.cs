using GBILET.Core.DTOs.Auth;

namespace GBILET.Core.Service.Auth;

public interface IAuthService
{
    Task<(bool Success, string? Error, AuthUserDto? User)> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error, AuthUserDto? User)> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error, AuthUserDto? User)> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> VerifyPhoneAsync(VerifyPhoneRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> RequestPasswordChangeAsync(Guid userId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> ConfirmPasswordChangeAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
}
