namespace GBILET.Core.Models.Flight;

public class UpdatePassengersResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Debug: Ham SOAP yaniti (gecici - production'da kaldirilacak)
    /// </summary>
    public string? RawSoapResponse { get; set; }

    /// <summary>
    /// Debug: Gonderilen SOAP request (gecici - production'da kaldirilacak)
    /// </summary>
    public string? RawSoapRequest { get; set; }
}
