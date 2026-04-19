using System.Text;
using GBILET.Core.Service.Admin;
using OtpNet;
using QRCoder;

namespace GBILET.Infrastructure.Services.Admin;

public class TotpService : ITotpService
{
    public string GenerateSecret()
    {
        // 20 byte (160 bit) — RFC 4226 önerisi
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string BuildOtpAuthUri(string secret, string username, string issuer = "ATABİLET")
    {
        // Standart otpauth URI:
        // otpauth://totp/<issuer>:<username>?secret=<secret>&issuer=<issuer>&algorithm=SHA1&digits=6&period=30
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedUsername = Uri.EscapeDataString(username);
        return $"otpauth://totp/{encodedIssuer}:{encodedUsername}" +
               $"?secret={secret}" +
               $"&issuer={encodedIssuer}" +
               $"&algorithm=SHA1&digits=6&period=30";
    }

    public string GenerateQrCodeBase64(string otpAuthUri)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
        using var pngQrCode = new PngByteQRCode(qrCodeData);
        var pngBytes = pngQrCode.GetGraphic(10);
        return "data:image/png;base64," + Convert.ToBase64String(pngBytes);
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return false;

        // Code 6 haneli digit olmalı
        code = code.Trim().Replace(" ", string.Empty);
        if (code.Length != 6 || !code.All(char.IsDigit))
            return false;

        byte[] keyBytes;
        try
        {
            keyBytes = Base32Encoding.ToBytes(secret);
        }
        catch
        {
            return false; // Secret bozuk
        }

        var totp = new Totp(keyBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);

        // VerificationWindow(1, 1) → ±30 saniye clock drift toleransı
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}