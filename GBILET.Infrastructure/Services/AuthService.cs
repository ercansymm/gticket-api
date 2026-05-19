using GBILET.Core.DTOs.Auth;
using GBILET.Core.Entities;
using GBILET.Core.Enums;
using GBILET.Core.Service.Auth;
using GBILET.Core.Service.Sms;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly GTicketDbContext _db;
    private readonly IOtpService _otp;
    private readonly ISmsService _sms;

    public AuthService(GTicketDbContext db, IOtpService otp, ISmsService sms)
    {
        _db  = db;
        _otp = otp;
        _sms = sms;
    }

    public async Task<(bool Success, string? Error, AuthUserDto? User)> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "E-posta ve şifre zorunludur.", null);

        if (string.IsNullOrWhiteSpace(request.FullName))
            return (false, "Ad Soyad zorunludur.", null);

        if (request.Password.Length < 6)
            return (false, "Şifre en az 6 karakter olmalıdır.", null);

        if (string.IsNullOrWhiteSpace(request.Phone))
            return (false, "Telefon numarası zorunludur.", null);

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return (false, "Bu e-posta zaten kayıtlı.", null);

        var user = new User
        {
            Id               = Guid.NewGuid(),
            Email            = email,
            FullName         = request.FullName.Trim(),
            Phone            = request.Phone.Trim(),
            IsPhoneVerified  = false,
            PasswordHash     = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
            Role             = "Customer",
            CustomerNumber   = "C" + DateTime.UtcNow.ToString("yyMMddHHmmssfff"),
            CreatedAt        = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // OTP gönder (fire-and-forget değil — hata olursa kullanıcı bilgilendirilsin)
        var code = await _otp.GenerateAndStoreAsync(user.Phone, OtpPurpose.PhoneVerification, ct);
        _ = _sms.SendOtpAsync(user.Phone, code, "kayit dogrulama", ct);

        return (true, null, new AuthUserDto
        {
            Id                        = user.Id.ToString(),
            Email                     = user.Email,
            Name                      = user.FullName,
            Role                      = user.Role,
            Token                     = string.Empty,
            RequiresPhoneVerification = true,
            MaskedPhone               = MaskPhone(user.Phone)
        });
    }

    public async Task<(bool Success, string? Error, AuthUserDto? User)> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "E-posta ve şifre zorunludur.", null);

        var email = request.Email.Trim().ToLowerInvariant();
        var user  = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
            return (false, "E-posta veya şifre hatalı.", null);

        bool valid;
        try { valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash); }
        catch { valid = false; }

        if (!valid)
            return (false, "E-posta veya şifre hatalı.", null);

        // Telefon doğrulanmamışsa OTP akışına yönlendir
        if (!user.IsPhoneVerified && !string.IsNullOrWhiteSpace(user.Phone))
        {
            var code = await _otp.GenerateAndStoreAsync(user.Phone, OtpPurpose.PhoneVerification, ct);
            _ = _sms.SendOtpAsync(user.Phone, code, "giris dogrulama", ct);

            return (true, null, new AuthUserDto
            {
                Id                        = user.Id.ToString(),
                Email                     = user.Email,
                Name                      = user.FullName,
                Role                      = user.Role,
                Token                     = string.Empty,
                RequiresPhoneVerification = true,
                MaskedPhone               = MaskPhone(user.Phone)
            });
        }

        return (true, null, ToDto(user));
    }

    public async Task<(bool Success, string? Error, AuthUserDto? User)> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return (false, "Google hesabında e-posta adresi bulunamadı.", null);

        var email = request.Email.Trim().ToLowerInvariant();
        var user  = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            user = new User
            {
                Id              = Guid.NewGuid(),
                Email           = email,
                FullName        = request.Name?.Trim() ?? email,
                PasswordHash    = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 12),
                Role            = "Customer",
                IsPhoneVerified = true, // Google OAuth zaten doğrulanmış hesap
                CustomerNumber  = "C" + DateTime.UtcNow.ToString("yyMMddHHmmssfff"),
                CreatedAt       = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
        }

        return (true, null, ToDto(user));
    }

    public async Task<(bool Success, string? Error)> VerifyPhoneAsync(VerifyPhoneRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return (false, "Doğrulama kodu zorunludur.");

        string? phone = request.Phone;

        // Email ile gelen isteklerde telefonu veritabanından çek
        if (string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(request.Email))
        {
            var u = await _db.Users.FirstOrDefaultAsync(
                x => x.Email == request.Email.Trim().ToLowerInvariant(), ct);
            if (u is null) return (false, "Hesap bulunamadı.");
            phone = u.Phone;
        }

        if (string.IsNullOrWhiteSpace(phone))
            return (false, "Telefon numarası belirlenemedi.");

        var result = await _otp.ValidateAsync(phone, request.Code, OtpPurpose.PhoneVerification, ct);

        return result.Status switch
        {
            OtpValidationStatus.Success => await MarkPhoneVerifiedAsync(phone, ct),
            OtpValidationStatus.InvalidCode =>
                (false, result.RemainingAttempts > 0
                    ? $"Kod hatalı. {result.RemainingAttempts} deneme hakkınız kaldı."
                    : "Kod hatalı. Deneme hakkınız doldu."),
            OtpValidationStatus.Expired            => (false, "Kodun süresi dolmuş. Yeni kod isteyin."),
            OtpValidationStatus.MaxAttemptsReached => (false, "Çok fazla hatalı giriş. Yeni kod isteyin."),
            _                                      => (false, "Geçersiz veya süresi dolmuş kod.")
        };
    }

    public async Task<(bool Success, string? Error)> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default)
    {
        string? phone = request.Phone;

        // Email ile gelen isteklerde telefonu veritabanından çek
        if (string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(request.Email))
        {
            var byEmail = await _db.Users.FirstOrDefaultAsync(
                u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);
            if (byEmail is null) return (false, "Bu e-posta ile kayıtlı hesap bulunamadı.");
            phone = byEmail.Phone;
        }

        if (string.IsNullOrWhiteSpace(phone))
            return (false, "Telefon numarası zorunludur.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == phone, ct);
        if (user is null)
            return (false, "Bu telefon numarasıyla kayıtlı hesap bulunamadı.");

        if (user.IsPhoneVerified)
            return (false, "Bu numara zaten doğrulanmış.");

        var code = await _otp.GenerateAndStoreAsync(phone, OtpPurpose.PhoneVerification, ct);
        _ = _sms.SendOtpAsync(phone, code, "kayit dogrulama", ct);

        return (true, null);
    }

    public async Task<(bool Success, string? Error, string? MaskedPhone)> ForgotPasswordAsync(
        ForgotPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return (false, "E-posta zorunludur.", null);

        var email = request.Email.Trim().ToLowerInvariant();
        var user  = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Güvenlik: kullanıcı bulunamasa bile hata vermiyoruz
        if (user is null || string.IsNullOrWhiteSpace(user.Phone))
            return (true, null, null);

        var code = await _otp.GenerateAndStoreAsync(user.Phone, OtpPurpose.ForgotPassword, ct);
        _ = _sms.SendOtpAsync(user.Phone, code, "sifre sifirlama", ct);

        return (true, null, MaskPhone(user.Phone));
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Code)  ||
            string.IsNullOrWhiteSpace(request.NewPassword))
            return (false, "Tüm alanlar zorunludur.");

        if (request.NewPassword.Length < 6)
            return (false, "Şifre en az 6 karakter olmalıdır.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user  = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || string.IsNullOrWhiteSpace(user.Phone))
            return (false, "Hesap bulunamadı.");

        var result = await _otp.ValidateAsync(user.Phone, request.Code, OtpPurpose.ForgotPassword, ct);

        if (result.Status != OtpValidationStatus.Success)
        {
            return result.Status switch
            {
                OtpValidationStatus.InvalidCode =>
                    (false, result.RemainingAttempts > 0
                        ? $"Kod hatalı. {result.RemainingAttempts} deneme hakkınız kaldı."
                        : "Kod hatalı. Deneme hakkınız doldu."),
                OtpValidationStatus.Expired            => (false, "Kodun süresi dolmuş. Yeni kod isteyin."),
                OtpValidationStatus.MaxAttemptsReached => (false, "Çok fazla hatalı giriş. Yeni kod isteyin."),
                _                                      => (false, "Geçersiz kod.")
            };
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RequestPasswordChangeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (user is null)
            return (false, "Kullanıcı bulunamadı.");

        if (string.IsNullOrWhiteSpace(user.Phone))
            return (false, "Hesabınızda kayıtlı telefon numarası yok.");

        var code = await _otp.GenerateAndStoreAsync(user.Phone, OtpPurpose.PasswordChange, ct);
        _ = _sms.SendOtpAsync(user.Phone, code, "sifre degistirme", ct);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ConfirmPasswordChangeAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.NewPassword))
            return (false, "Kod ve yeni şifre zorunludur.");

        if (request.NewPassword.Length < 6)
            return (false, "Şifre en az 6 karakter olmalıdır.");

        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (user is null)
            return (false, "Kullanıcı bulunamadı.");

        if (string.IsNullOrWhiteSpace(user.Phone))
            return (false, "Hesabınızda kayıtlı telefon numarası yok.");

        var result = await _otp.ValidateAsync(user.Phone, request.Code, OtpPurpose.PasswordChange, ct);

        if (result.Status != OtpValidationStatus.Success)
        {
            return result.Status switch
            {
                OtpValidationStatus.InvalidCode =>
                    (false, result.RemainingAttempts > 0
                        ? $"Kod hatalı. {result.RemainingAttempts} deneme hakkınız kaldı."
                        : "Kod hatalı. Deneme hakkınız doldu."),
                OtpValidationStatus.Expired            => (false, "Kodun süresi dolmuş. Yeni kod isteyin."),
                OtpValidationStatus.MaxAttemptsReached => (false, "Çok fazla hatalı giriş. Yeni kod isteyin."),
                _                                      => (false, "Geçersiz kod.")
            };
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
        await _db.SaveChangesAsync(ct);

        return (true, null);
    }

    // --- Helpers ---

    private async Task<(bool, string?)> MarkPhoneVerifiedAsync(string phone, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == phone, ct);
        if (user is null) return (false, "Kullanıcı bulunamadı.");

        user.IsPhoneVerified = true;
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    private static AuthUserDto ToDto(User u) => new()
    {
        Id    = u.Id.ToString(),
        Email = u.Email,
        Name  = u.FullName,
        Role  = u.Role,
        Token = Guid.NewGuid().ToString("N")
    };

    // "+905321234567" → "+90 532 *** **67"
    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "***";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return "***";
        return phone[..^4] + "****";
    }
}
