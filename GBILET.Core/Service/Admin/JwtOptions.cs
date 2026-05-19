namespace GBILET.Core.Service.Admin;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "atabilet.com";
    public string Audience { get; set; } = "admin.atabilet.com";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
    public string PrivateKeyPath { get; set; } = null!;
    public string PublicKeyPath { get; set; } = null!;
}