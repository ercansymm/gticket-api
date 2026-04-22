using GBILET.Core.DTOs.Auth;
using GBILET.Core.Entities;
using GBILET.Core.Service.Auth;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly GTicketDbContext _db;

    public AuthService(GTicketDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string? Error, AuthUserDto? User)> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "E-posta ve şifre zorunludur.", null);

        if (string.IsNullOrWhiteSpace(request.FullName))
            return (false, "Ad Soyad zorunludur.", null);

        if (request.Password.Length < 6)
            return (false, "Şifre en az 6 karakter olmalıdır.", null);

        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(u => u.Email == email, ct);
        if (exists)
            return (false, "Bu e-posta zaten kayıtlı.", null);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
            Role = "Customer",
            CustomerNumber = "C" + DateTime.UtcNow.ToString("yyMMddHHmmssfff"),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return (true, null, ToDto(user));
    }

    public async Task<(bool Success, string? Error, AuthUserDto? User)> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "E-posta ve şifre zorunludur.", null);

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
            return (false, "E-posta veya şifre hatalı.", null);

        bool valid;
        try
        {
            valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch
        {
            valid = false;
        }

        if (!valid)
            return (false, "E-posta veya şifre hatalı.", null);

        return (true, null, ToDto(user));
    }

    private static AuthUserDto ToDto(User u) => new()
    {
        Id = u.Id.ToString(),
        Email = u.Email,
        Name = u.FullName,
        Role = u.Role,
        // Simple opaque token for downstream calls; NextAuth wraps its own JWT around this.
        Token = Guid.NewGuid().ToString("N")
    };
}
