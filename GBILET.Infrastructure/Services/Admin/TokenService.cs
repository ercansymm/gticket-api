using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GBILET.Core.Entities.Admin;
using GBILET.Core.Service.Admin;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GBILET.Infrastructure.Services.Admin;

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly RsaSecurityKey _privateKey;
    private readonly RsaSecurityKey _publicKey;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.PrivateKeyPath))
            throw new InvalidOperationException("Jwt:PrivateKeyPath not configured. Set via user-secrets or environment variable.");

        if (!File.Exists(_options.PrivateKeyPath))
            throw new FileNotFoundException($"JWT private key not found: {_options.PrivateKeyPath}");

        if (!File.Exists(_options.PublicKeyPath))
            throw new FileNotFoundException($"JWT public key not found: {_options.PublicKeyPath}");

        // Private key — PEM'den yükle
        var privateRsa = RSA.Create();
        privateRsa.ImportFromPem(File.ReadAllText(_options.PrivateKeyPath));
        _privateKey = new RsaSecurityKey(privateRsa);

        // Public key — PEM'den yükle
        var publicRsa = RSA.Create();
        publicRsa.ImportFromPem(File.ReadAllText(_options.PublicKeyPath));
        _publicKey = new RsaSecurityKey(publicRsa);
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(AdminUser user)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("fullName", user.FullName),
            new("jti", Guid.NewGuid().ToString()) // Unique token ID
        };

        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expiresAt,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = new SigningCredentials(_privateKey, SecurityAlgorithms.RsaSha256)
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return (token, expiresAt);
    }

    public string GenerateRefreshToken()
    {
        // 64 byte cryptographically secure random — base64 ile ~86 karakter
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public string HashToken(string token)
    {
        // SHA-256 — refresh token DB'de hash olarak tutulur (BCrypt gerekmiyor,
        // zaten yüksek-entropy random string, timing attack riski yok)
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public ClaimsPrincipal? ValidateAccessTokenIgnoringExpiry(string token)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _publicKey,
            ValidateLifetime = false, // Expired token'ı da kabul et (refresh flow için)
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var handler = new JsonWebTokenHandler();
            var result = handler.ValidateTokenAsync(token, validationParameters).GetAwaiter().GetResult();
            if (!result.IsValid) return null;

            return new ClaimsPrincipal(result.ClaimsIdentity);
        }
        catch
        {
            return null;
        }
    }
}