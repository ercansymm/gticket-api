namespace GBILET.Core.Exceptions;

/// <summary>
/// BiletBank tarafindan donen oturum (sessionId/sessionToken) suresi dolmus
/// veya gecersiz oldugu durumda firlatilir. Cagiran katman re-search yapip
/// yeni session ile islemi tekrar deneyebilir.
/// </summary>
public class BiletBankSessionExpiredException : Exception
{
    public string? SessionId { get; }
    public string? OperationName { get; }

    public BiletBankSessionExpiredException(string message)
        : base(message) { }

    public BiletBankSessionExpiredException(string message, string? sessionId, string? operationName)
        : base(message)
    {
        SessionId = sessionId;
        OperationName = operationName;
    }

    public BiletBankSessionExpiredException(string message, Exception inner)
        : base(message, inner) { }
}
