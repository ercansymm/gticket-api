using System;
namespace GBILET.Core.Models.Flight;

public class LoginResponse
{
    public string? SessionId { get; set; }
    public string? SessionToken { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ServiceError { get; set; }
    public UserInfo? UserInfo { get; set; }
}

public class UserInfo
{
    public string? BusinessId { get; set; }
    public string? BusinessName { get; set; }
    public string? CountryCode { get; set; }
    public bool IfBusinessRoot { get; set; }
    public string? UserEmail { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}