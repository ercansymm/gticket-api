using System.Text;
using System.Xml;
using GBILET.Core.Service.Flight;

namespace GBILET.Infrastructure.Services;

public class BiletBankFlightService : IFlightService
{
    private readonly HttpClient _httpClient;
    private const string WsUrl = "http://37.148.212.253/TrevooWS.svc";
    private const string Username = "GTRAVELAPI";
    private const string Password = "Gtravel123!";

    public BiletBankFlightService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private async Task<string> LoginAsync()
    {
        var soapBody = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" 
               xmlns:tns=""http://tempuri.org/"">
    <soap:Body>
        <tns:Login>
            <tns:username>{Username}</tns:username>
            <tns:password>{Password}</tns:password>
        </tns:Login>
    </soap:Body>
</soap:Envelope>";

        var content = new StringContent(soapBody, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", "http://tempuri.org/IService/Login");

        var response = await _httpClient.PostAsync(WsUrl, content);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync();

        // Token'ı XML'den parse et
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        // Namespace manager
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");

        // Token field adı BiletBank'a göre değişebilir, ham XML'i loglayalım
        return xml;
    }

    public async Task<string> SearchFlight(string from, string to)
    {
        // 1. Login ol
        var loginXml = await LoginAsync();

        // Login başarılı mı kontrol et (ham XML'i döndür debug için)
        return loginXml;
    }
}