using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Xml.Linq;

namespace GBILET.Infrastructure.Services;

public class BiletBankFlightService : IFlightService
{
    private readonly HttpClient _httpClient;
    private readonly string _clientName;
    private readonly string _password;
    private readonly string _username;
    private readonly string _proxyUrl;

    public BiletBankFlightService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        _clientName = configuration["BiletBank:ClientName"];
        _password = configuration["BiletBank:Password"];
        _username = configuration["BiletBank:Username"];
        _proxyUrl = configuration["BiletBank:Url"];
    }

    public async Task<string> SearchFlight(string from, string to)
    {
        var loginResult = await LoginAsync();

        if (loginResult.HasError)
        {
            return $"Login hatası: {loginResult.ErrorMessage}";
        }

        var searchResult = await SearchAsync(
            loginResult.SessionId,
            loginResult.SessionToken,
            from,
            to
        );

        return searchResult;
    }

    private async Task<LoginResponse> LoginAsync()
    {
        var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base"">
<soap:Body>
<tem:Login>
<tem:request>
<trev1:Form>
<trev1:ChannelCode>2</trev1:ChannelCode>
<trev1:ClientIP></trev1:ClientIP>
<trev1:ClientName>{_clientName}</trev1:ClientName>
<trev1:Password>{_password}</trev1:Password>
<trev1:Username>{_username}</trev1:Username>
</trev1:Form>
</tem:request>
</tem:Login>
</soap:Body>
</soap:Envelope>";

        try
        {
            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/ITrevooWS/Login");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            Console.WriteLine("=== LOGIN RESPONSE ===");
            Console.WriteLine(responseText);

            var doc = XDocument.Parse(responseText);

            var sessionId = doc.GetValue("SessionId");
            var sessionToken = doc.GetValue("SessionToken");
            var hasError = doc.GetValue("HasError");

            return new LoginResponse
            {
                SessionId = sessionId,
                SessionToken = sessionToken,
                HasError = hasError == "true",
                ErrorMessage = hasError == "true" ? doc.GetValue("Message") : null
            };
        }
        catch (Exception ex)
        {
            return new LoginResponse
            {
                HasError = true,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<string> SearchAsync(
        string sessionId,
        string sessionToken,
        string from,
        string to)
    {
        var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:trev2=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Shopping"">
<soap:Body>
<tem:AirSearch>
<tem:request>

<trev:AuthenticationHeader>
<trev:SessionId>{sessionId}</trev:SessionId>
<trev:SessionToken>{sessionToken}</trev:SessionToken>
</trev:AuthenticationHeader>

<trev:ExtraParamList>
<trev:ExtendedData>
<trev:Name>BrandedFareVersion</trev:Name>
<trev:Value>v2</trev:Value>
</trev:ExtendedData>
<trev:ExtendedData>
<trev:Name>SearchReason</trev:Name>
<trev:Value>SearchAndBook</trev:Value>
</trev:ExtendedData>
</trev:ExtraParamList>

<trev1:Form>

<trev2:FlightType>OW</trev2:FlightType>

<trev2:Options>
<trev2:FlightClass>Economy</trev2:FlightClass>
<trev2:IfDirectFlightsOnly>false</trev2:IfDirectFlightsOnly>
<trev2:IfRefundablesOnly>false</trev2:IfRefundablesOnly>
<trev2:SearchTimeoutMilliseconds>30000</trev2:SearchTimeoutMilliseconds>
</trev2:Options>

<trev2:PaxItems>
<trev2:T_AirSearch_PaxItem>
<trev2:PaxCode>ADT</trev2:PaxCode>
<trev2:PaxCount>1</trev2:PaxCount>
</trev2:T_AirSearch_PaxItem>
</trev2:PaxItems>

<trev2:Segments>
<trev2:T_AirSearch_SegmentItem>

<trev2:DepartureDay>{DateTime.Now.AddDays(7):yyyy-MM-dd}T00:00:00</trev2:DepartureDay>

<trev2:Origin>
<trev2:Code>{from}</trev2:Code>
<trev2:CountryCode>TR</trev2:CountryCode>
<trev2:IsCity>false</trev2:IsCity>
</trev2:Origin>

<trev2:Destination>
<trev2:Code>{to}</trev2:Code>
<trev2:CountryCode>TR</trev2:CountryCode>
<trev2:IsCity>false</trev2:IsCity>
</trev2:Destination>

<trev2:SequenceNo>1</trev2:SequenceNo>

</trev2:T_AirSearch_SegmentItem>
</trev2:Segments>

</trev1:Form>

</tem:request>
</tem:AirSearch>
</soap:Body>
</soap:Envelope>";

        var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", "http://tempuri.org/ITrevooWS/AirSearch");

        var response = await _httpClient.PostAsync(_proxyUrl, content);
        var responseText = await response.Content.ReadAsStringAsync();

        return responseText;
    }
}