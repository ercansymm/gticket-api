namespace GBILET.Core.Models.Flight;

public class AllocateRequest
{
    /// <summary>
    /// Orijinal arama parametreleri (Allocate oncesi session'da arama olusturmak icin gerekli)
    /// </summary>
    public SearchRequest SearchRequest { get; set; } = null!;

    /// <summary>
    /// Search sonucundan secilen urun ID'si (FlightOption.ProductId)
    /// </summary>
    public string ProductId { get; set; } = null!;

    /// <summary>
    /// Servis ucreti / satici komisyonu. Komisyon hesaplamasi gerekmiyorsa 0 gonderin.
    /// Response'ta LastSellerCommission olarak yansir.
    /// </summary>
    public decimal SelectedServiceFee { get; set; } = 0;
}
