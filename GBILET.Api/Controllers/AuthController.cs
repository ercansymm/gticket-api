using GBILET.Core.DTOs.Auth;
using GBILET.Core.Service.Auth;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet("test")]
        public IActionResult Test() => Ok("API çalışıyor");

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
        {
            var (success, error, user) = await _authService.RegisterAsync(request, ct);
            if (!success)
                return BadRequest(new { error });
            return Ok(user);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            var (success, error, user) = await _authService.LoginAsync(request, ct);
            if (!success)
                return Unauthorized(new { error });
            return Ok(user);
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct)
        {
            var (success, error, user) = await _authService.GoogleLoginAsync(request, ct);
            if (!success)
                return BadRequest(new { error });
            return Ok(user);
        }

        /// <summary>Kayıt / giriş sonrası telefon OTP doğrulaması.</summary>
        [HttpPost("verify-phone")]
        public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneRequest request, CancellationToken ct)
        {
            var (success, error) = await _authService.VerifyPhoneAsync(request, ct);
            if (!success)
                return BadRequest(new { error });
            return Ok(new { message = "Telefon doğrulandı." });
        }

        /// <summary>OTP'yi yeniden gönderir (süresi dolmuş veya kaybolmuş kodlar için).</summary>
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request, CancellationToken ct)
        {
            var (success, error) = await _authService.ResendOtpAsync(request, ct);
            if (!success)
                return BadRequest(new { error });
            return Ok(new { message = "Doğrulama kodu tekrar gönderildi." });
        }

        /// <summary>
        /// Giriş yapmış kullanıcı şifre değiştirme OTP'si ister.
        /// BFF, X-User-Id header'ı ile kullanıcı kimliğini iletir.
        /// </summary>
        [HttpPost("request-password-change")]
        public async Task<IActionResult> RequestPasswordChange(CancellationToken ct)
        {
            var userId = GetUserIdFromHeader();
            if (userId == Guid.Empty)
                return BadRequest(new { error = "Geçersiz kullanıcı kimliği." });

            var (success, error) = await _authService.RequestPasswordChangeAsync(userId, ct);
            if (!success)
                return BadRequest(new { error });
            return Ok(new { message = "Doğrulama kodu telefonunuza gönderildi." });
        }

        /// <summary>OTP + yeni şifre ile şifre değişimini tamamlar.</summary>
        [HttpPost("confirm-password-change")]
        public async Task<IActionResult> ConfirmPasswordChange([FromBody] ChangePasswordRequest request, CancellationToken ct)
        {
            var userId = GetUserIdFromHeader();
            if (userId == Guid.Empty)
                return BadRequest(new { error = "Geçersiz kullanıcı kimliği." });

            var (success, error) = await _authService.ConfirmPasswordChangeAsync(userId, request, ct);
            if (!success)
                return BadRequest(new { error });
            return Ok(new { message = "Şifreniz başarıyla güncellendi." });
        }

        /// <summary>Telefon numarası bilinmeden şifre sıfırlama OTP'si gönderir (misafir akışı).</summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
        {
            var (success, error, maskedPhone) = await _authService.ForgotPasswordAsync(request, ct);
            if (!success)
                return BadRequest(new { error });
            return Ok(new { message = "Kod gönderildi.", maskedPhone });
        }

        /// <summary>OTP + yeni şifre ile şifre sıfırlamayı tamamlar (giriş yapmadan).</summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
        {
            var (success, error) = await _authService.ResetPasswordAsync(request, ct);
            if (!success)
                return BadRequest(new { error });
            return Ok(new { message = "Şifreniz başarıyla güncellendi." });
        }

        private Guid GetUserIdFromHeader()
        {
            var header = Request.Headers["X-User-Id"].FirstOrDefault();
            return Guid.TryParse(header, out var id) ? id : Guid.Empty;
        }
    }
}
