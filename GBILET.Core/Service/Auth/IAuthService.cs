using GBILET.Core.DTOs.Auth;

namespace GBILET.Core.Service.Auth;

public interface IAuthService
{
    Task<(bool Success, string? Error, AuthUserDto? User)> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error, AuthUserDto? User)> LoginAsync(LoginRequest request, CancellationToken ct = default);
}
