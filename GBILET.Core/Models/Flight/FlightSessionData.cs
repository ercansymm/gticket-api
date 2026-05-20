using GBILET.Core.Service.Flight;

namespace GBILET.Core.Models.Flight;

public class FlightSessionData
{
    public string? SearchId { get; set; }
    public string? ShoppingFileId { get; set; }
    public string? SessionId { get; set; }
    public string? SessionToken { get; set; }

    /// <summary>
    /// Orijinal search kriterleri. Allocate sırasında BiletBank recoverable bir hata dönerse
    /// (session expired, product not available, shopping file invalid vs.) backend bu kriterlerle
    /// transparent retry için yeni Login+AirSearch+Allocate zincirini başlatır.
    /// </summary>
    public SearchRequest? SearchRequest { get; set; }

    // MakePreBooking sonrasi guncellenen alanlar
    public string? ProductId { get; set; }
    public string? BookingCode { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal TotalFare { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal ServiceFee { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public Guid? BookingId { get; set; }

    /// <summary>
    /// Her FlightOption.ProductId için BB'nin döndürdüğü acente komisyonu (CustomerCommission.Value).
    /// Yolcu sayısı ile çarpılıp tüm yolcular için toplanmış değerdir. Allocate request'inde
    /// SelectedServiceFee.Amount alanına yazılır; aksi halde BB acente payını sıfırlar.
    /// </summary>
    public Dictionary<string, decimal> CustomerCommissionByProductId { get; set; } = new();
}
