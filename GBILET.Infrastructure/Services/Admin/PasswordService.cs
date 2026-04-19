using BCrypt.Net;
using GBILET.Core.Service.Admin;

namespace GBILET.Infrastructure.Services.Admin;

public class PasswordService : IPasswordService
{
    private const int WorkFactor = 12; // Industry standard 2026

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (SaltParseException)
        {
            // Hash formatı geçersiz — sessizce false döner
            return false;
        }
    }

    public (bool IsValid, string? ErrorMessage) ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Parola boş olamaz.");

        if (password.Length < 12)
            return (false, "Parola en az 12 karakter olmalıdır.");

        if (!password.Any(char.IsUpper))
            return (false, "Parola en az bir büyük harf içermelidir.");

        if (!password.Any(char.IsLower))
            return (false, "Parola en az bir küçük harf içermelidir.");

        if (!password.Any(char.IsDigit))
            return (false, "Parola en az bir rakam içermelidir.");

        if (password.All(char.IsLetterOrDigit))
            return (false, "Parola en az bir özel karakter içermelidir (!@#$%^&* vb.).");

        return (true, null);
    }
}