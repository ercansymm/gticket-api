namespace GBILET.Core.Service.Admin;

public interface ITotpService
{
    /// <summary>
    /// Yeni TOTP secret üretir (Base32). AdminUser.TwoFactorSecret'a kaydedilir.
    /// </summary>
    string GenerateSecret();

    /// <summary>
    /// Google Authenticator uyumlu otpauth:// URI üretir. QR code bundan oluşturulur.
    /// </summary>
    string BuildOtpAuthUri(string secret, string username, string issuer = "ATABİLET");

    /// <summary>
    /// otpauth URI'den base64 PNG QR code üretir (frontend'e img src olarak gönderilir).
    /// </summary>
    string GenerateQrCodeBase64(string otpAuthUri);

    /// <summary>
    /// Kullanıcının girdiği 6 haneli kodu mevcut zamana göre doğrular.
    /// Clock drift için ±1 window tolerance var.
    /// </summary>
    bool VerifyCode(string secret, string code);
}