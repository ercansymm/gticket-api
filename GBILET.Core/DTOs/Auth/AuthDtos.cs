namespace GBILET.Core.DTOs.Auth;

public class RegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "Agent";
    public string Token { get; set; } = string.Empty;
    public bool RequiresPhoneVerification { get; set; } = false;
    public string? MaskedPhone { get; set; }
}

public class GoogleLoginRequest
{
    public string GoogleId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Picture { get; set; }
}

public class VerifyPhoneRequest
{
    public string? Phone { get; set; }
    // Email ile alternatif arama (login OTP akışında kullanılır)
    public string? Email { get; set; }
    public string Code { get; set; } = string.Empty;
}

public class ResendOtpRequest
{
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class ChangePasswordRequest
{
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
