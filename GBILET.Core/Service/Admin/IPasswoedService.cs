namespace GBILET.Core.Service.Admin;

public interface IPasswordService
{
    /// <summary>
    /// BCrypt ile parolayı hash'ler (work factor 12).
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verilen parolanın hash ile eşleşip eşleşmediğini kontrol eder.
    /// </summary>
    bool VerifyPassword(string password, string hash);

    /// <summary>
    /// Parolanın minimum güvenlik kriterlerini karşılayıp karşılamadığını kontrol eder.
    /// Min 12 karakter, büyük+küçük harf, rakam, özel karakter.
    /// </summary>
    (bool IsValid, string? ErrorMessage) ValidatePasswordStrength(string password);
}