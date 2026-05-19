namespace GBILET.Core.Models.Flight;

public class FlightSessionData
{
    public string? SearchId { get; set; }
    public string? ShoppingFileId { get; set; }
    public string? SessionId { get; set; }
    public string? SessionToken { get; set; }

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
}
