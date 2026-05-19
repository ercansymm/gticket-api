namespace GBILET.Core.Helpers;

/// <summary>
/// Payment.Status (Success/Failed/Pending3D/Pending) → frontend kategorisine map'ler.
/// </summary>
public static class PaymentStatusMapper
{
    public static string CategoryOf(string status)
    {
        return status switch
        {
            "Success" => "success",
            "Failed" => "failed",
            "Pending3D" or "Pending" => "pending",
            _ => "pending"
        };
    }

    /// <summary>
    /// Bilinen ErrorCode değerlerini Türkçe etikete çevirir.
    /// </summary>
    public static string LabelForErrorCode(string? code)
    {
        return code switch
        {
            "CardLimit" => "Kart limiti yetersiz",
            "ThreeDFailed" => "3D şifre hatası",
            "BankRejected" => "Banka reddetti",
            "Timeout" => "Bağlantı hatası",
            "InvalidCard" => "Kart geçersiz",
            "Other" => "Diğer",
            _ => "Diğer"
        };
    }

    public static readonly string[] KnownErrorCodes = new[]
    {
        "CardLimit", "ThreeDFailed", "BankRejected", "Timeout", "InvalidCard", "Other"
    };
}
