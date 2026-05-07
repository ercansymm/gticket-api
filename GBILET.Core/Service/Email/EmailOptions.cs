namespace GBILET.Core.Service.Email;

public class EmailOptions
{
    public const string SectionName = "Email";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 465;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = "Atabilet Destek";
    public string FromEmail { get; set; } = string.Empty;
}
