namespace GBILET.Core.Models.Flight;

public class RemoveProductResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Kaldirma sonrasi shopping dosya durumu
    /// </summary>
    public string? ShoppingFileId { get; set; }
}
