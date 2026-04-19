using GBILET.Core.Entities.Admin;
using GBILET.Core.Service.Admin;
using GBILET.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Api.Controllers.Admin;

/// <summary>
/// One-time setup endpoint. Sadece Development ortamında ve hiç admin yokken çalışır.
/// İlk SuperAdmin oluşturulduktan sonra otomatik devre dışı kalır.
/// Production'a deploy etmeden ÖNCE bu controller silinmeli.
/// </summary>
[ApiController]
[Route("api/admin/bootstrap")]
public class AdminBootstrapController : ControllerBase
{
    private readonly GTicketDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IWebHostEnvironment _env;

    public AdminBootstrapController(
        GTicketDbContext db,
        IPasswordService passwords,
        IWebHostEnvironment env)
    {
        _db = db;
        _passwords = passwords;
        _env = env;
    }

    public record BootstrapRequest(
        string Username,
        string Email,
        string FullName,
        string Password
    );

    [HttpPost("create-first-superadmin")]
    public async Task<IActionResult> CreateFirstSuperAdmin([FromBody] BootstrapRequest request)
    {
        // Güvenlik 1 — sadece development'ta
        if (!_env.IsDevelopment())
            return NotFound();

        // Güvenlik 2 — sadece HİÇ admin yoksa
        if (await _db.AdminUsers.AnyAsync())
            return Conflict(new { error = "Admin kullanıcısı zaten var. Bootstrap kullanılamaz." });

        // Validation
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            return BadRequest(new { error = "Username en az 3 karakter olmalı." });

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest(new { error = "Geçerli bir e-posta gerekli." });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { error = "Ad Soyad gerekli." });

        var passwordCheck = _passwords.ValidatePasswordStrength(request.Password);
        if (!passwordCheck.IsValid)
            return BadRequest(new { error = passwordCheck.ErrorMessage });

        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim().ToLowerInvariant(),
            Email = request.Email.Trim().ToLowerInvariant(),
            FullName = request.FullName.Trim(),
            PasswordHash = _passwords.HashPassword(request.Password),
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            TwoFactorEnabled = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.AdminUsers.Add(admin);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "İlk SuperAdmin oluşturuldu. Bu endpoint artık çalışmaz.",
            id = admin.Id,
            username = admin.Username,
            email = admin.Email,
            role = admin.Role.ToString()
        });
    }
}