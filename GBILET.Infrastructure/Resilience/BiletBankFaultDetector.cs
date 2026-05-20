using GBILET.Core.Exceptions;

namespace GBILET.Infrastructure.Resilience;

/// <summary>
/// BiletBank SOAP yanitlarinda session expire / yetkilendirme hatasi pattern'larini
/// tespit eden yardimci sinif. SOAP client'in ic implementasyonunu degistirmeden,
/// servis metotlarinin sonunda response inceleme yoluyla session exception firlatmak icin kullanilir.
/// </summary>
public static class BiletBankFaultDetector
{
    private static readonly string[] SessionKeywords =
    {
        "session",
        "expired",
        "timeout",
        "not authenticated",
        "login required",
        "invalid token",
        "authentication failed"
    };

    /// <summary>
    /// BiletBank allocate akışında "transient stale state" niteliğinde olan hata pattern'ları.
    /// Bunlar genellikle aynı session/shoppingFile üzerinden art arda farklı uçuş seçildiğinde
    /// veya BiletBank tarafında ürün durumu güncellendiğinde dönüyor. Yeni Login+AirSearch+Allocate
    /// ile çoğunlukla başarıyla recover olurlar.
    /// </summary>
    private static readonly string[] RecoverableAllocateKeywords =
    {
        "product not found",
        "product not available",
        "no longer available",
        "no availability",
        "shopping file",
        "shoppingfile",
        "shopping_file",
        "allocate failed",
        "allocation failed",
        "no seats",
        "soldout",
        "sold out",
        "selected allocated",
        "already allocated",          // BB exact: "Already allocated product"
        "already selected"
    };

    /// <summary>
    /// SOAP Fault veya error message string'inde session-expire keyword'lerini arar.
    /// Case-insensitive arama yapar.
    /// </summary>
    public static bool IsSessionExpiredFault(string? faultString)
    {
        if (string.IsNullOrWhiteSpace(faultString)) return false;

        foreach (var kw in SessionKeywords)
        {
            if (faultString.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// Exception zincirinde session expire belirtilerini arar:
    /// - HttpRequestException ile 401/403
    /// - "There is an error in XML document (1, 1)" pattern (BiletBank session expire'in tipik belirtisi)
    /// - SessionKeywords mesaj icinde
    /// </summary>
    public static bool IsSessionExpiredError(Exception? ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            var msg = current.Message ?? string.Empty;

            if (msg.Contains("There is an error in XML document (1, 1)", StringComparison.OrdinalIgnoreCase))
                return true;

            if (msg.Contains("401") && msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                return true;

            if (msg.Contains("403") && msg.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
                return true;

            if (IsSessionExpiredFault(msg)) return true;
        }
        return false;
    }

    /// <summary>
    /// HTTP status code 401/403 ise session expired kabul eder.
    /// </summary>
    public static bool IsSessionExpiredStatus(int httpStatusCode)
        => httpStatusCode == 401 || httpStatusCode == 403;

    /// <summary>
    /// HasError=true ve message session keyword'lerini iceriyorsa BiletBankSessionExpiredException firlatir.
    /// Aksi halde no-op.
    /// </summary>
    public static void ThrowIfSessionExpired(bool hasError, string? errorMessage, string operationName, string? sessionId = null)
    {
        if (!hasError) return;
        if (!IsSessionExpiredFault(errorMessage)) return;

        throw new BiletBankSessionExpiredException(
            $"BiletBank session expired during {operationName}: {errorMessage}",
            sessionId,
            operationName);
    }

    /// <summary>
    /// Allocate akışında recoverable kabul edilen hata pattern'ı tespit edilir.
    /// </summary>
    public static bool IsRecoverableAllocateFault(string? faultString)
    {
        if (string.IsNullOrWhiteSpace(faultString)) return false;

        foreach (var kw in RecoverableAllocateKeywords)
        {
            if (faultString.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// HasError=true ve message recoverable allocate keyword'lerini iceriyorsa BiletBankSessionExpiredException
    /// firlatir. SessionRecoveryExecutor bu exception'i yakalayip yeni Login+AirSearch+Allocate ile recovery yapar.
    /// Exception adi semantik olarak "session expired" olsa da retry mekanizmasi generic davraniyor.
    /// </summary>
    public static void ThrowIfRecoverableAllocateFault(bool hasError, string? errorMessage, string operationName, string? sessionId = null)
    {
        if (!hasError) return;
        if (!IsRecoverableAllocateFault(errorMessage)) return;

        throw new BiletBankSessionExpiredException(
            $"BiletBank recoverable allocate fault during {operationName}: {errorMessage}",
            sessionId,
            operationName);
    }
}
