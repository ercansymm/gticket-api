using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security;
using System.Text;
using System.Xml.Linq;


namespace GBILET.Infrastructure.Services;

public class BiletBankFlightService : IFlightService
{
    /// <summary>
    /// BOM'suz UTF-8 encoding � bazi SOAP servisleri BOM (EF BB BF) gordu�unde
    /// "There is an error in XML document (1, 2)" hatasi verir.
    /// </summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// SOAP request icin ByteArrayContent olusturur.
    /// StringContent yerine kullanilir cunku:
    /// 1. BOM eklenmez (UTF8Encoding(false))
    /// 2. Content-Type header'i tam olarak kontrol edilir
    /// 3. Bazi WCF servisleri charset parametresinde sorun yasayabiliyor
    /// </summary>
    private static ByteArrayContent CreateSoapContent(string soapXml, string soapAction)
    {
        var bytes = Utf8NoBom.GetBytes(soapXml);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml")
        {
            CharSet = "utf-8"
        };
        content.Headers.TryAddWithoutValidation("SOAPAction", soapAction);
        return content;
    }

    private readonly HttpClient _httpClient;
    private readonly ILogger<BiletBankFlightService> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _clientName;
    private readonly string _password;
    private readonly string _username;
    private readonly string _proxyUrl;

    public BiletBankFlightService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BiletBankFlightService> logger,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;

        _clientName = configuration["BiletBank:ClientName"]!;
        _password = configuration["BiletBank:Password"]!;
        _username = configuration["BiletBank:Username"]!;
        _proxyUrl = configuration["BiletBank:Url"]!;
    }

    public async Task<AirSearchResponse> SearchFlightAsync(SearchRequest request)
    {
        var loginResult = await LoginAsync();

        if (loginResult.HasError)
        {
            return new AirSearchResponse
            {
                HasError = true,
                ErrorMessage = $"Login hatası: {loginResult.ErrorMessage}"
            };
        }

        var sessionId = loginResult.SessionId!;
        var sessionToken = loginResult.SessionToken!;

        // Comma-separated origin/destination desteği: her IATA kodu için ayrı AirSearch yap
        var origins = request.Origin.Contains(',')
            ? request.Origin.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { request.Origin };

        var destinations = request.Destination.Contains(',')
            ? request.Destination.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { request.Destination };

        // Tek origin + tek destination → normal akış
        if (origins.Length == 1 && destinations.Length == 1)
        {
            var response = await AirSearchAsync(sessionId, sessionToken, request);

            // Transient BiletBank errors (e.g. TripTypeIsInvalidOrMissing on first call):
            // retry once with a fresh login session.
            if (response.HasError && response.ErrorMessage != null
                && response.ErrorMessage.Contains("TripType", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "[SearchFlight] Transient TripType error — retrying with fresh session. Error: {Error}",
                    response.ErrorMessage);

                var retryLogin = await LoginAsync();
                if (!retryLogin.HasError)
                {
                    sessionId = retryLogin.SessionId!;
                    sessionToken = retryLogin.SessionToken!;
                    response = await AirSearchAsync(sessionId, sessionToken, request);
                }
            }

            response.SessionId = sessionId;
            response.SessionToken = sessionToken;
            return response;
        }

        // Birden fazla origin/destination → sıralı arama + sonuçları birleştir
        // NOT: BiletBank aynı sessionId ile eş zamanlı birden fazla istek gelince
        // "SessionLockDuplicateCall" hatası döner. Bu yüzden Task.WhenAll yerine
        // sıralı (sequential) await kullanılıyor — RT aramalarda lock süresi daha uzun
        // olduğundan paralel çalışmada bu hata RT için düzenli olarak oluşuyordu.
        var results = new List<AirSearchResponse>();
        foreach (var origin in origins)
        {
            foreach (var destination in destinations)
            {
                var singleRequest = new SearchRequest
                {
                    Origin = origin,
                    Destination = destination,
                    OriginCountryCode = request.OriginCountryCode,
                    DestinationCountryCode = request.DestinationCountryCode,
                    // City group birden fazla havalimanına bölününce her biri tekil airport kodudur,
                    // dolayısıyla IsCity=false olmalı (IsCity=true gönderilirse BiletBank şehir kodu
                    // olarak arar, bulamaz ve 0 sonuç döner).
                    OriginIsCity = false,
                    DestinationIsCity = false,
                    DepartureDate = request.DepartureDate,
                    ReturnDate = request.ReturnDate,
                    FlightType = request.FlightType,
                    FlightClass = request.FlightClass,
                    AdultCount = request.AdultCount,
                    ChildCount = request.ChildCount,
                    InfantCount = request.InfantCount,
                    DirectFlightsOnly = request.DirectFlightsOnly,
                    RefundablesOnly = request.RefundablesOnly,
                    SearchTimeoutMilliseconds = request.SearchTimeoutMilliseconds,
                    PreferredAirlines = request.PreferredAirlines,
                    SearchReason = request.SearchReason,
                };

                _logger.LogInformation(
                    "[SearchFlight] Sequential sub-search: {Origin} → {Destination}",
                    origin, destination);

                var subResult = await AirSearchAsync(sessionId, sessionToken, singleRequest);
                results.Add(subResult);
            }
        }

        // Sonuçları birleştir
        var merged = MergeAirSearchResponses(results.ToArray(), sessionId, sessionToken);
        return merged;
    }

    /// <summary>
    /// Birden fazla AirSearch sonucunu birleştirir ve flight number + departure time'a göre deduplicate eder.
    /// </summary>
    private AirSearchResponse MergeAirSearchResponses(AirSearchResponse[] responses, string sessionId, string sessionToken)
    {
        var merged = new AirSearchResponse
        {
            HasError = false,
            SessionId = sessionId,
            SessionToken = sessionToken,
        };

        // İlk başarılı response'tan SearchId ve ShoppingFileId al
        var firstSuccess = responses.FirstOrDefault(r => !r.HasError);
        if (firstSuccess != null)
        {
            merged.SearchId = firstSuccess.SearchId;
            merged.ShoppingFileId = firstSuccess.ShoppingFileId;
        }

        // Tüm başarısızsa hata dön
        if (responses.All(r => r.HasError))
        {
            merged.HasError = true;
            merged.ErrorMessage = responses.FirstOrDefault(r => r.ErrorMessage != null)?.ErrorMessage
                ?? "Tüm arama istekleri başarısız oldu.";
            return merged;
        }

        // Başarısız sub-search'lerin hata mesajlarını topla (partial failure için tanı)
        var subErrors = responses
            .Where(r => r.HasError && !string.IsNullOrEmpty(r.ErrorMessage))
            .Select(r => r.ErrorMessage!)
            .Distinct()
            .ToList();

        if (subErrors.Count > 0)
        {
            merged.SubSearchErrors = subErrors;
            _logger.LogWarning(
                "[MergeAirSearch] {ErrorCount}/{TotalResponses} sub-search(es) failed. Errors: {Errors}",
                subErrors.Count, responses.Length, string.Join(" | ", subErrors));
        }

        // FlightOption'ları birleştir ve deduplicate et
        var seen = new HashSet<string>();
        foreach (var resp in responses.Where(r => !r.HasError))
        {
            _logger.LogInformation(
                "[MergeAirSearch] Sub-search OK → FlightOptions: {Fo}, RecommendationBoxes: {Rb}",
                resp.FlightOptions.Count, resp.RecommendationBoxes.Count);

            foreach (var fo in resp.FlightOptions)
            {
                var key = BuildFlightDeduplicationKey(fo);
                if (seen.Add(key))
                {
                    merged.FlightOptions.Add(fo);
                }
            }

            foreach (var rb in resp.RecommendationBoxes)
            {
                merged.RecommendationBoxes.Add(rb);
            }
        }

        _logger.LogInformation(
            "[MergeAirSearch] {TotalResponses} response merged → {FlightCount} unique flights, {RbCount} recommendation boxes",
            responses.Length, merged.FlightOptions.Count, merged.RecommendationBoxes.Count);

        return merged;
    }

    /// <summary>
    /// FlightOption için deduplicate anahtarı oluşturur: flight number + departure time.
    /// </summary>
    private static string BuildFlightDeduplicationKey(FlightOption fo)
    {
        if (fo.Segments.Count == 0)
            return fo.ProductId ?? Guid.NewGuid().ToString();

        return string.Join("|", fo.Segments.Select(s =>
            $"{s.MarketingAirline}{s.FlightNumber}_{s.DepartureDay}_{s.DepartureTime}_{s.OriginCode}_{s.DestinationCode}"));
    }

    public async Task<FlightSearchResponseDto> SearchFlightDtoAsync(SearchRequest request)
    {
        var rawResponse = await SearchFlightAsync(request);
        var dto = FlightSearchMapper.MapToDto(rawResponse, _logger, request.FlightClass);

        // Session bilgilerini cache'le (sonraki ad�mlarda allocate/booking i�in)
        if (!rawResponse.HasError && !string.IsNullOrEmpty(rawResponse.SearchId))
        {
            var cacheKey = $"flight_session_{rawResponse.SearchId}";
            var sessionData = new FlightSessionData
            {
                SearchId = rawResponse.SearchId,
                ShoppingFileId = rawResponse.ShoppingFileId,
                SessionId = rawResponse.SessionId,
                SessionToken = rawResponse.SessionToken
            };

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(20));

            _cache.Set(cacheKey, sessionData, cacheOptions);

            _logger.LogInformation(
                "[SearchFlightDto] Session cached: SearchId={SearchId}, SessionId={SessionId}",
                rawResponse.SearchId, rawResponse.SessionId);
        }

        return dto;
    }

    public async Task<AllocateResponse> AllocateFlightAsync(AllocateRequest request)
    {
        string sessionId;
        string sessionToken;

        // Kullanici search'ten alinan session bilgisini gonderdiyse dogrudan kullan
        if (!string.IsNullOrEmpty(request.SessionId) && !string.IsNullOrEmpty(request.SessionToken))
        {
            sessionId = request.SessionId;
            sessionToken = request.SessionToken;
        }
        else
        {
            // Session yoksa login + search yap
            if (request.SearchRequest == null)
            {
                return new AllocateResponse
                {
                    HasError = true,
                    ErrorMessage = "SessionId/SessionToken verilmediyse SearchRequest zorunludur."
                };
            }

            var loginResult = await LoginAsync();
            if (loginResult.HasError)
            {
                return new AllocateResponse
                {
                    HasError = true,
                    ErrorMessage = $"Login hatasi: {loginResult.ErrorMessage}"
                };
            }

            var searchResponse = await AirSearchAsync(loginResult.SessionId!, loginResult.SessionToken!, request.SearchRequest);
            if (searchResponse.HasError)
            {
                return new AllocateResponse
                {
                    HasError = true,
                    ErrorMessage = $"Search hatasi (allocate oncesi): {searchResponse.ErrorMessage}"
                };
            }

            sessionId = loginResult.SessionId!;
            sessionToken = loginResult.SessionToken!;
        }

        var response = await AllocateAsync(sessionId, sessionToken, request);
        response.SessionId = sessionId;
        response.SessionToken = sessionToken;

        return response;
    }

    public async Task<UpdatePassengersResponse> UpdatePassengersAsync(UpdatePassengersRequest request)
    {
        // TempTag bos gelen yolcularda PaxReferenceId'yi TempTag olarak ata
        foreach (var pax in request.Passengers)
        {
            if (string.IsNullOrEmpty(pax.TempTag) && !string.IsNullOrEmpty(pax.PaxReferenceId))
                pax.TempTag = pax.PaxReferenceId;
        }

        // Telefon numaras�n� BiletBank format�na normalize et
        if (request.Contact != null)
            request.Contact.Phone = NormalizePhone(request.Contact.Phone);

        // Yolcu bilgilerini logla — debug icin kritik
        _logger.LogInformation(
            "[UpdatePassengers] Yolcu sayisi: {Count}, Tipler: {Types}, SequenceNo'lar: {SeqNos}, TempTag'ler: {TempTags}",
            request.Passengers.Count,
            string.Join(", ", request.Passengers.Select(p => p.PaxType)),
            string.Join(", ", request.Passengers.Select(p => p.SequenceNo)),
            string.Join(", ", request.Passengers.Select(p => p.TempTag ?? "(null)")));

        var inner = await UpdatePassengersInternalAsync(request.SessionId, request.SessionToken, request);
        return inner;
    }

    /// <summary>
    /// Telefon numaras�n� BiletBank'�n bekledi�i +90XXXXXXXXXX format�na d�n��t�r�r.
    /// Kabul edilen giri�ler: 5351234567, 05351234567, 905351234567, 90-5351234567, +90-5351234567, +905351234567
    /// </summary>
    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "+905000000000";

        // Sadece rakamlar� al
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // 905351234567 (12 hane) � 5351234567
        if (digits.Length == 12 && digits.StartsWith("90"))
            digits = digits[2..];

        // 05351234567 (11 hane, 0 ile ba�l�yor) � 5351234567
        if (digits.Length == 11 && digits.StartsWith("0"))
            digits = digits[1..];

        // 5351234567 (10 hane) � +905351234567
        if (digits.Length == 10)
            return $"+90{digits}";

        // Di�er durumlarda orijinal de�eri + ile ba�lat
        return phone.StartsWith("+") ? phone : $"+{phone}";
    }

    private async Task<LoginResponse> LoginAsync()
    {
        var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Authentication.IO"">
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
            _logger.LogInformation("[Login] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Authentication/Login");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Login] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[Login] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResponse
                {
                    HasError = true,
                    ErrorMessage = $"Login HTTP {(int)response.StatusCode}: {responseText}"
                };
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception xmlEx)
            {
                _logger.LogError(xmlEx, "[Login] XML parse hatasi. Response XML degil. Ilk 500 karakter: {ResponseStart}",
                    responseText.Length > 500 ? responseText[..500] : responseText);
                return new LoginResponse
                {
                    HasError = true,
                    ErrorMessage = $"Login: Servis yaniti XML olarak parse edilemedi. Hata: {xmlEx.Message}"
                };
            }

            var hasError = doc.GetValue("HasError");

            var loginResponse = new LoginResponse
            {
                SessionId = doc.GetValue("SessionId"),
                SessionToken = doc.GetValue("SessionToken"),
                HasError = hasError == "true" || string.IsNullOrEmpty(doc.GetValue("SessionId")),
                ErrorMessage = hasError == "true" ? doc.GetValue("Message") : null,
                ServiceError = doc.GetValue("ServiceError")
            };

            if (string.IsNullOrEmpty(loginResponse.SessionId))
            {
                loginResponse.HasError = true;
                loginResponse.ErrorMessage ??= "Login yan�t�nda SessionId bulunamad�.";
            }

            _logger.LogInformation("[Login] SessionId: {SessionId}, HasError: {HasError}",
                loginResponse.SessionId, loginResponse.HasError);

            var userInfoElement = doc.GetDescendants("UserInfo").FirstOrDefault();
            if (userInfoElement != null)
            {
                loginResponse.UserInfo = new UserInfo
                {
                    BusinessId = userInfoElement.GetValue("BusinessId"),
                    BusinessName = userInfoElement.GetValue("BusinessName"),
                    CountryCode = userInfoElement.GetValue("CountryCode"),
                    IfBusinessRoot = userInfoElement.GetBoolValue("IfBusinessRoot"),
                    UserEmail = userInfoElement.GetValue("UserEmail"),
                    UserId = userInfoElement.GetValue("UserId"),
                    UserName = userInfoElement.GetValue("UserName")
                };
            }

            return loginResponse;
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? "No inner exception";
            var innerInner = ex.InnerException?.InnerException?.Message ?? "No inner-inner exception";
            return new LoginResponse
            {
                HasError = true,
                ErrorMessage = $"{ex.Message} | Inner: {innerMsg} | Inner2: {innerInner}"
            };
        }
    }

    private async Task<AirSearchResponse> AirSearchAsync(
        string sessionId,
        string sessionToken,
        SearchRequest request)
    {
        var soapRequest = BuildAirSearchSoapRequest(sessionId, sessionToken, request);

        try
        {
            _logger.LogInformation("[AirSearch] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Shopping/AirSearch");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[AirSearch] SOAP Response:\n{SoapResponse}", responseText);

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception xmlEx)
            {
                _logger.LogError(xmlEx, "[AirSearch] XML parse hatasi. Response XML degil. Ilk 500 karakter: {ResponseStart}",
                    responseText.Length > 500 ? responseText[..500] : responseText);
                return new AirSearchResponse
                {
                    HasError = true,
                    ErrorMessage = $"AirSearch: Servis yaniti XML olarak parse edilemedi. Hata: {xmlEx.Message}"
                };
            }

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                // BiletBank Message element may contain nested sub-elements whose text
                // all get concatenated by XElement.Value. Extract only the first direct
                // text node or the <Text>/<Description> child if present for a clean message.
                var msgEl = doc.Descendants()
                    .FirstOrDefault(x => x.Name.LocalName == "Message");
                string? errorMessage = null;
                if (msgEl != null)
                {
                    // Prefer a <Text> or <Description> child
                    var textChild = msgEl.Elements()
                        .FirstOrDefault(e => e.Name.LocalName is "Text" or "Description");
                    errorMessage = textChild != null
                        ? textChild.Value
                        : (msgEl.HasElements
                            ? msgEl.Elements().First().Value  // first child text
                            : msgEl.Value);                    // leaf text
                }
                errorMessage ??= doc.GetValue("ServiceError");

                return new AirSearchResponse
                {
                    HasError = true,
                    ErrorMessage = errorMessage
                };
            }

            // DEBUG: XML yapisini dosyaya yaz — BrandedFares nerede geliyor?
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);

                // Tum benzersiz element isimlerini topla
                var allElements = doc.Descendants().Select(x => x.Name.LocalName).Distinct().OrderBy(x => x).ToList();

                // BrandedFares/BrandedFareItem/BrandedItem iceren elementleri bul
                var brandedElements = doc.Descendants()
                    .Where(x => x.Name.LocalName.Contains("Branded", StringComparison.OrdinalIgnoreCase)
                             || x.Name.LocalName.Contains("Brand", StringComparison.OrdinalIgnoreCase))
                    .Select(x => $"{x.Name.LocalName} (parent: {x.Parent?.Name.LocalName})")
                    .Distinct()
                    .ToList();

                // T_FlightOption sayisi ve icindeki BrandedFares durumu
                var flightOptions = doc.Descendants().Where(x => x.Name.LocalName == "T_FlightOption").ToList();
                var foWithBranded = flightOptions.Count(fo =>
                    fo.Elements().Any(e => e.Name.LocalName == "BrandedFares") ||
                    fo.Descendants().Any(e => e.Name.LocalName == "BrandedFares"));

                // T_RecommendationBox sayisi ve icindeki BrandedFares durumu
                var recBoxes = doc.Descendants().Where(x => x.Name.LocalName == "T_RecommendationBox").ToList();
                var rbWithBranded = recBoxes.Count(rb =>
                    rb.Elements().Any(e => e.Name.LocalName == "BrandedFares") ||
                    rb.Descendants().Any(e => e.Name.LocalName == "BrandedFares"));

                // Ilk T_FlightOption'un XML yapisini kaydet (debug icin)
                var firstFO = flightOptions.FirstOrDefault()?.ToString() ?? "YOK";
                if (firstFO.Length > 3000) firstFO = firstFO[..3000] + "...[TRUNCATED]";

                // Ilk T_RecommendationBox'un XML yapisini kaydet
                var firstRB = recBoxes.FirstOrDefault()?.ToString() ?? "YOK";
                if (firstRB.Length > 10000) firstRB = firstRB[..10000] + "...[TRUNCATED]";

                var debugLog = $"""
=== AirSearch XML DEBUG {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} ===
Toplam benzersiz element: {allElements.Count}
Element isimleri: {string.Join(", ", allElements)}

--- Brand iceren elementler ---
{(brandedElements.Count > 0 ? string.Join("\n", brandedElements) : "HICBIRI YOK")}

--- T_FlightOption ---
Toplam: {flightOptions.Count}
BrandedFares iceren: {foWithBranded}

--- T_RecommendationBox ---
Toplam: {recBoxes.Count}
BrandedFares iceren: {rbWithBranded}

--- Ilk T_FlightOption XML ---
{firstFO}

--- Ilk T_RecommendationBox XML ---
{firstRB}
=== END ===

""";
                File.AppendAllText(Path.Combine(logDir, "airsearch-debug.log"), debugLog);
            }
            catch { /* debug log yazma hatasi kritik degil */ }

            return ParseAirSearchResponse(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AirSearch] Exception");
            return new AirSearchResponse
            {
                HasError = true,
                ErrorMessage = $"AirSearch hatas\u0131: {ex.Message}"
            };
        }
    }

    private static string BuildAirSearchSoapRequest(
        string sessionId,
        string sessionToken,
        SearchRequest request)
    {
        var paxItems = new StringBuilder();
        if (request.AdultCount > 0)
        {
            paxItems.Append($@"
               <trev2:T_AirSearch_PaxItem>
                  <trev2:PaxCode>ADT</trev2:PaxCode>
                  <trev2:PaxCount>{request.AdultCount}</trev2:PaxCount>
               </trev2:T_AirSearch_PaxItem>");
        }
        if (request.ChildCount > 0)
        {
            paxItems.Append($@"
               <trev2:T_AirSearch_PaxItem>
                  <trev2:PaxCode>CHD</trev2:PaxCode>
                  <trev2:PaxCount>{request.ChildCount}</trev2:PaxCount>
               </trev2:T_AirSearch_PaxItem>");
        }
        if (request.InfantCount > 0)
        {
            paxItems.Append($@"
               <trev2:T_AirSearch_PaxItem>
                  <trev2:PaxCode>INF</trev2:PaxCode>
                  <trev2:PaxCount>{request.InfantCount}</trev2:PaxCount>
               </trev2:T_AirSearch_PaxItem>");
        }

        var segments = new StringBuilder();

        if (request.FlightType == "MP" && request.Segments is { Count: >= 2 })
        {
            // Multi-city: her bacak için ayrı T_AirSearch_SegmentItem
            for (var i = 0; i < request.Segments.Count; i++)
            {
                var seg = request.Segments[i];
                segments.Append($@"
                <trev2:T_AirSearch_SegmentItem>
                   <trev2:DepartureDay>{seg.DepartureDate:yyyy-MM-dd}T00:00:00.000+00:00</trev2:DepartureDay>
                   <trev2:Destination>
                      <trev2:Code>{seg.Destination}</trev2:Code>
                      <trev2:CountryCode>{seg.DestinationCountryCode}</trev2:CountryCode>
                      <trev2:IsCity>{seg.DestinationIsCity.ToString().ToLowerInvariant()}</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Destination>
                   <trev2:Origin>
                      <trev2:Code>{seg.Origin}</trev2:Code>
                      <trev2:CountryCode>{seg.OriginCountryCode}</trev2:CountryCode>
                      <trev2:IsCity>{seg.OriginIsCity.ToString().ToLowerInvariant()}</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Origin>
                   <trev2:SequenceNo>{i + 1}</trev2:SequenceNo>
                </trev2:T_AirSearch_SegmentItem>");
            }
        }
        else
        {
            // OW / RT
            segments.Append($@"
                <trev2:T_AirSearch_SegmentItem>
                   <trev2:DepartureDay>{request.DepartureDate:yyyy-MM-dd}T00:00:00.000+00:00</trev2:DepartureDay>
                   <trev2:Destination>
                      <trev2:Code>{request.Destination}</trev2:Code>
                      <trev2:CountryCode>{request.DestinationCountryCode}</trev2:CountryCode>
                      <trev2:IsCity>{request.DestinationIsCity.ToString().ToLowerInvariant()}</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Destination>
                   <trev2:Origin>
                      <trev2:Code>{request.Origin}</trev2:Code>
                      <trev2:CountryCode>{request.OriginCountryCode}</trev2:CountryCode>
                      <trev2:IsCity>{request.OriginIsCity.ToString().ToLowerInvariant()}</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Origin>
                   <trev2:SequenceNo>1</trev2:SequenceNo>
                </trev2:T_AirSearch_SegmentItem>");

            if (request.FlightType == "RT" && request.ReturnDate.HasValue)
            {
                segments.Append($@"
                <trev2:T_AirSearch_SegmentItem>
                   <trev2:DepartureDay>{request.ReturnDate.Value:yyyy-MM-dd}T00:00:00.000+00:00</trev2:DepartureDay>
                   <trev2:Destination>
                      <trev2:Code>{request.Origin}</trev2:Code>
                      <trev2:CountryCode>{request.OriginCountryCode}</trev2:CountryCode>
                      <trev2:IsCity>{request.OriginIsCity.ToString().ToLowerInvariant()}</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Destination>
                   <trev2:Origin>
                      <trev2:Code>{request.Destination}</trev2:Code>
                      <trev2:CountryCode>{request.DestinationCountryCode}</trev2:CountryCode>
                      <trev2:IsCity>{request.DestinationIsCity.ToString().ToLowerInvariant()}</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Origin>
                   <trev2:SequenceNo>2</trev2:SequenceNo>
                </trev2:T_AirSearch_SegmentItem>");
            }
        }

        var preferredAirlines = string.Empty;
        if (request.PreferredAirlines is { Count: > 0 })
        {
            var airlinesXml = string.Join("", request.PreferredAirlines
                .Select(a => $"<arr:string>{a}</arr:string>"));
            preferredAirlines = $@"
               <trev2:PreferedAirlines xmlns:arr=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"">
                  {airlinesXml}
               </trev2:PreferedAirlines>";
        }

        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:trev2=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Air"">
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
               <trev:Type>true</trev:Type>
               <trev:Value>v2</trev:Value>
            </trev:ExtendedData>
            <trev:ExtendedData>
               <trev:Name>SearchReason</trev:Name>
               <trev:Value>{request.SearchReason}</trev:Value>
            </trev:ExtendedData>
         </trev:ExtraParamList>
         <trev1:Form>
            <trev2:FlightType>{request.FlightType}</trev2:FlightType>
            <trev2:Options>
               <trev2:FlightClass>{request.FlightClass}</trev2:FlightClass>
                <trev2:IfDirectFlightsOnly>{request.DirectFlightsOnly.ToString().ToLowerInvariant()}</trev2:IfDirectFlightsOnly>
               <trev2:IfRefundablesOnly>{request.RefundablesOnly.ToString().ToLowerInvariant()}</trev2:IfRefundablesOnly>
               <trev2:SearchTimeoutMilliseconds>{request.SearchTimeoutMilliseconds}</trev2:SearchTimeoutMilliseconds>{preferredAirlines}
            </trev2:Options>
            <trev2:PaxItems>{paxItems}
            </trev2:PaxItems>
            <trev2:Segments>{segments}
            </trev2:Segments>
         </trev1:Form>
      </tem:request>
   </tem:AirSearch>
</soap:Body>
</soap:Envelope>";
    }

    private static AirSearchResponse ParseAirSearchResponse(XDocument doc)
    {
        var response = new AirSearchResponse
        {
            HasError = false,
            SearchId = doc.GetValue("SearchId"),
            ShoppingFileId = doc.GetValue("ShoppingFileId")
        };

        // DEBUG: XML'deki tum benzersiz element isimlerini topla
        response.DebugElementNames = doc.Descendants()
            .Select(x => x.Name.LocalName)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var flightOptionElements = doc.GetDescendants("T_FlightOption").ToList();
        foreach (var fo in flightOptionElements)
        {
            response.FlightOptions.Add(ParseFlightOption(fo));
        }

        // DEBUG: Ilk T_FlightOption'un ham XML'i
        if (flightOptionElements.Count > 0)
        {
            var xml = flightOptionElements[0].ToString();
            response.DebugFirstFlightOptionXml = xml.Length > 2000 ? xml[..2000] : xml;
        }

        var recommendationBoxElements = doc.GetDescendants("T_RecommendationBox").ToList();
        foreach (var rb in recommendationBoxElements)
        {
            response.RecommendationBoxes.Add(ParseRecommendationBox(rb));
        }

        // DEBUG: Ilk T_RecommendationBox'un ham XML'i
        if (recommendationBoxElements.Count > 0)
        {
            var xml = recommendationBoxElements[0].ToString();
            response.DebugFirstRecommendationBoxXml = xml.Length > 2000 ? xml[..2000] : xml;
        }

        return response;
    }

    private static FlightOption ParseFlightOption(XElement fo)
    {
        var option = new FlightOption
        {
            ProductId = fo.GetValue("ProductId"),
            ProductItemId = fo.GetValue("ProductItemId"),
            ProviderId = fo.GetValue("ProviderId"),
            Type = fo.GetValue("Type"),
            BookingCode = fo.GetValue("BookingCode"),
            Currency = fo.GetValue("Currency"),
            ExchangeCode = fo.GetValue("ExchangeCode"),
            BaseFare = fo.GetDecimalValue("BaseFare"),
            Taxes = fo.GetDecimalValue("Taxes"),
            ServiceFee = fo.GetDecimalValue("ServiceFee"),
            SystemServiceFee = fo.GetDecimalValue("SystemServiceFee"),
            EndSellerCommission = fo.GetDecimalValue("EndSellerCommission"),
            NetFare = fo.GetDecimalValue("NetFare"),
            TotalFare = fo.GetDecimalValue("TotalFare"),
            IsRefundable = fo.GetBoolValue("IsRefundable"),
            IsReservable = fo.GetBoolValue("IsReservable"),
            IsGetFareRulesEnabled = fo.GetBoolValue("IsGetFareRulesEnabled"),
            OptionFlag = fo.GetValue("OptionFlag"),
            BookingProvider = fo.GetValue("BookingProvider"),
            BookingProviderId = fo.GetValue("BookingProviderId"),
            Duration = fo.GetIntValue("Duration")
        };

        foreach (var seg in fo.GetDescendants("T_Segment"))
        {
            option.Segments.Add(ParseSegment(seg));
        }

        foreach (var sa in fo.GetDescendants("T_SegmentAvailability"))
        {
            option.SegmentAvailabilities.Add(new SegmentAvailability
            {
                SegmentSequenceNo = sa.GetIntValue("SegmentSequenceNo"),
                BookingClassCode = sa.GetValue("BookingClassCode"),
                AvailableSeats = sa.GetIntValue("AvailableSeats"),
                IsPromo = sa.GetBoolValue("IsPromo")
            });
        }

        foreach (var pfi in fo.GetDescendants("T_PaxFareItem"))
        {
            option.PassengerFareItems.Add(ParsePassengerFareItem(pfi));
        }

        // BrandedFares: v1 yapısında doğrudan "BrandedFares", v2'de "T_BrandedFare_v2" olabilir.
        // Ayrıca T_FlightOption'ın doğrudan çocuğu olmayabilir — GetDescendants kullan.
        var brandedFaresElement = fo.GetElement("BrandedFares")
            ?? fo.GetDescendants("BrandedFares").FirstOrDefault()
            ?? fo.GetDescendants("T_BrandedFare_v2").FirstOrDefault();
        if (brandedFaresElement != null)
        {
            // v2 yapısında: BrandedFareItems (çoğul container) > BrandedFareItem
            // v1 yapısında: doğrudan BrandedFareItem
            // GetDescendants her iki durumu da yakalar
            foreach (var bfi in brandedFaresElement.GetDescendants("BrandedFareItem"))
            {
                option.BrandedFareItems.Add(ParseBrandedFareItem(bfi));
            }

            // BrandedItem'ları BrandId'ye göre doğru BrandedFareItem'a eşleştir
            // v2 yapısında: BrandedItems (çoğul container) > BrandedItem
            foreach (var bi in brandedFaresElement.GetDescendants("BrandedItem"))
            {
                var brandedItem = ParseBrandedItem(bi);
                var matchedFareItem = option.BrandedFareItems.FirstOrDefault(bfi =>
                    bfi.BrandedFarePassengers.Any(p =>
                        p.FareComponents.Any(fc => fc.BrandId == brandedItem.BrandId)));
                if (matchedFareItem != null)
                {
                    matchedFareItem.BrandedItems.Add(brandedItem);
                }
                else
                {
                    // Eşleşme bulunamadıysa tüm BrandedFareItem'lara ekle (fallback)
                    foreach (var bfi in option.BrandedFareItems)
                        bfi.BrandedItems.Add(brandedItem);
                }
            }
        }

        // FreeBaggageAllowance: v2'de FreeBaggageAllowances (çoğul container) altında olabilir
        var baggageElement = fo.GetElement("FreeBaggageAllowance")
            ?? fo.GetDescendants("FreeBaggageAllowance").FirstOrDefault()
            ?? fo.GetDescendants("FreeBaggageAllowances").FirstOrDefault();
        if (baggageElement != null)
        {
            foreach (var pba in baggageElement.GetDescendants("PaxBaggageAllowance"))
            {
                option.FreeBaggageAllowances.Add(new FreeBaggageAllowance
                {
                    Allowance = pba.GetValue("Allowance"),
                    Category = pba.GetValue("Category"),
                    Type = pba.GetValue("Type"),
                    Unit = pba.GetValue("Unit"),
                    PaxType = pba.GetValue("PaxType")
                });
            }
        }

        return option;
    }

    private static FlightSegment ParseSegment(XElement seg)
    {
        // Fallback: T_Segment (OW) ve A_FlightSegment (RT) farklı element isimleri kullanıyor
        var rawDepartureTime = seg.GetValue("DepartureTime");
        var rawArrivalTime = seg.GetValue("ArrivalTime");
        var rawDuration = seg.GetValue("Duration") ?? seg.GetValue("FlightDuration");

        return new FlightSegment
        {
            SegmentId = seg.GetValue("SegmentId"),
            SequenceNo = seg.GetIntValue("SequenceNo"),
            OriginCode = seg.GetValue("OriginCode") ?? seg.GetValue("DepartureAirport"),
            DestinationCode = seg.GetValue("DestinationCode") ?? seg.GetValue("ArrivalAirport"),
            OD_OriginCode = seg.GetValue("OD_OriginCode"),
            OD_DestinationCode = seg.GetValue("OD_DestinationCode"),
            DepartureDay = FormatDay(seg.GetValue("DepartureDay") ?? seg.GetValue("DepartureDate")),
            DepartureTime = FormatIso8601DurationAsTime(rawDepartureTime),
            ArrivalDay = FormatDay(seg.GetValue("ArrivalDay") ?? seg.GetValue("ArrivalDate")),
            ArrivalTime = FormatIso8601DurationAsTime(rawArrivalTime),
            MarketingAirline = seg.GetValue("MarketingAirline"),
            OperatingAirline = seg.GetValue("OperatingAirline"),
            FlightNumber = seg.GetValue("FlightNumber"),
            BookingClass = seg.GetValue("BookingClass"),
            FareBasis = seg.GetValue("FareBasis"),
            FareType = seg.GetValue("FareType"),
            Equipment = seg.GetValue("Equipment"),
            Duration = ParseIso8601DurationToMinutes(rawDuration),
            SelectedBrandedFareItemId = seg.GetValue("SelectedBrandedFareItemId")
        };
    }

    private static (int hours, int minutes) ParseIso8601Duration(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith("PT"))
            return (0, 0);

        int hours = 0, minutes = 0;
        var span = value.AsSpan(2); // skip "PT"
        var numberStart = -1;

        for (int i = 0; i < span.Length; i++)
        {
            if (char.IsDigit(span[i]))
            {
                if (numberStart == -1) numberStart = i;
            }
            else
            {
                if (numberStart == -1) continue;
                var num = int.Parse(span[numberStart..i]);
                if (span[i] == 'H') hours = num;
                else if (span[i] == 'M') minutes = num;
                numberStart = -1;
            }
        }

        return (hours, minutes);
    }

    private static int ParseIso8601DurationToMinutes(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        if (int.TryParse(value, out var plainMinutes))
            return plainMinutes;

        var (h, m) = ParseIso8601Duration(value);
        return h * 60 + m;
    }

    private static string? FormatIso8601DurationAsTime(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (!value.StartsWith("PT"))
            return value;

        var (h, m) = ParseIso8601Duration(value);
        return $"{h:D2}:{m:D2}";
    }

    private static string? FormatDay(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (DateTime.TryParse(value, out var dt))
            return dt.ToString("yyyy-MM-dd");

        return value;
    }

    private static PassengerFareItem ParsePassengerFareItem(XElement pfi)
    {
        var item = new PassengerFareItem
        {
            PaxCode = pfi.GetValue("PaxCode"),
            PaxSequence = pfi.GetIntValue("PaxSequence"),
            ProductItemId = pfi.GetValue("ProductItemId"),
            Currency = pfi.GetValue("Currency"),
            BaseFare = pfi.GetDecimalValue("BaseFare"),
            Taxes = pfi.GetDecimalValue("Taxes"),
            ServiceFee = pfi.GetDecimalValue("ServiceFee"),
            SystemServiceFee = pfi.GetDecimalValue("SystemServiceFee"),
            EndSellerCommission = pfi.GetDecimalValue("EndSellerCommission"),
            TotalFare = pfi.GetDecimalValue("TotalFare")
        };

        var commission = pfi.GetElement("CustomerCommission");
        if (commission != null)
        {
            item.CustomerCommission = new CustomerCommission
            {
                Minimum = commission.GetDecimalValue("Minimum"),
                Maximum = commission.GetDecimalValue("Maximum"),
                Value = commission.GetDecimalValue("Value")
            };
        }

        return item;
    }

    private static BrandedFareItem ParseBrandedFareItem(XElement bfi)
    {
        var item = new BrandedFareItem
        {
            BrandedFareItemId = bfi.GetValue("BrandedFareItemId")
        };

        foreach (var bfp in bfi.GetDescendants("BrandedFarePassenger"))
        {
            var passenger = new BrandedFarePassenger
            {
                PassengerCount = bfp.GetIntValue("PassengerCount"),
                PassengerType = bfp.GetValue("PassengerType")
            };

            foreach (var fc in bfp.GetDescendants("FareComponent"))
            {
                passenger.FareComponents.Add(new FareComponent
                {
                    BrandId = fc.GetValue("BrandId"),
                    BookingClass = fc.GetValue("BookingClass"),
                    CabinClass = fc.GetValue("CabinClass"),
                    FareBasisCode = fc.GetValue("FareBasisCode"),
                    FreeBaggageAllowanceId = fc.GetValue("FreeBaggageAllowanceId"),
                    AvailableSeats = fc.GetIntValue("AvailableSeats"),
                    SegmentId = fc.GetValue("SegmentId")
                });
            }

            var fareInfo = bfp.GetElement("PassengerFareInfo");
            if (fareInfo != null)
            {
                passenger.PassengerFareInfo = new PassengerFareInfo
                {
                    BaseFare = fareInfo.GetDecimalValue("BaseFare"),
                    Taxes = fareInfo.GetDecimalValue("Taxes"),
                    TotalFare = fareInfo.GetDecimalValue("TotalFare"),
                    Currency = fareInfo.GetValue("Currency"),
                    PaxSequence = fareInfo.GetIntValue("PaxSequence"),
                    PaxType = fareInfo.GetValue("PaxType")
                };
            }

            var policy = bfp.GetElement("Policy");
            if (policy != null)
            {
                passenger.Policy = new FarePolicy();

                foreach (var cp in policy.GetDescendants("CancellationPolicy"))
                {
                    passenger.Policy.CancellationPolicies.Add(new CancellationPolicy
                    {
                        Amount = cp.GetDecimalValue("Amount"),
                        Applicability = cp.GetValue("Applicability"),
                        MinutesToDeparture = cp.GetIntValue("MinutesToDeparture"),
                        Currency = cp.GetValue("Currency"),
                        IsRefundable = cp.GetBoolValue("IsRefundable")
                    });
                }

                foreach (var chp in policy.GetDescendants("ChangePolicy"))
                {
                    passenger.Policy.ChangePolicies.Add(new ChangePolicy
                    {
                        Amount = chp.GetDecimalValue("Amount"),
                        Applicability = chp.GetValue("Applicability"),
                        MinutesToDeparture = chp.GetIntValue("MinutesToDeparture"),
                        Currency = chp.GetValue("Currency"),
                        IsChangeable = chp.GetBoolValue("IsChangeable")
                    });
                }
            }

            item.BrandedFarePassengers.Add(passenger);
        }

        var totalInfo = bfi.GetElement("TotalFareInfo");
        if (totalInfo != null)
        {
            item.TotalFareInfo = new BrandedFareTotalInfo
            {
                TotalFare = totalInfo.GetDecimalValue("TotalFare"),
                TotalTaxes = totalInfo.GetDecimalValue("TotalTaxes")
            };
        }

        return item;
    }

    private static BrandedItem ParseBrandedItem(XElement bi)
    {
        var item = new BrandedItem
        {
            BrandCode = bi.GetValue("BrandCode"),
            BrandId = bi.GetValue("BrandId"),
            BrandName = bi.GetValue("BrandName")
        };

        foreach (var rule in bi.GetDescendants("BrandedRule"))
        {
            item.BrandedRules.Add(new BrandedRule
            {
                Application = rule.GetValue("Application"),
                DisplayType = rule.GetValue("DisplayType"),
                RuleDescription = rule.GetValue("RuleDescription"),
                ServiceGroup = rule.GetValue("ServiceGroup")
            });
        }

        return item;
    }

    private static RecommendationBox ParseRecommendationBox(XElement rb)
    {
        // DEBUG: RB'nin tüm direct child element isimlerini logla — OtherFlights element adını keşfetmek için
        var childElementNames = rb.Elements().Select(e => e.Name.LocalName).ToList();
        try
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "rb-children-debug.log"),
                $"[{DateTime.UtcNow:HH:mm:ss}] RB children: {string.Join(", ", childElementNames)}\n");
        }
        catch { /* debug log yazma hatası kritik değil */ }

        var box = new RecommendationBox
        {
            ProductId = rb.GetValue("ProductId"),
            FlightId = rb.GetValue("FlightId"),
            Currency = rb.GetValue("Currency"),
            BaseFare = rb.GetDecimalValue("BaseFare"),
            Taxes = rb.GetDecimalValue("Taxes"),
            ServiceFee = rb.GetDecimalValue("ServiceFee"),
            TotalFare = rb.GetDecimalValue("TotalFare")
        };

        var departureFlights = rb.GetElement("DepartureFlights");
        if (departureFlights != null)
        {
            foreach (var of in departureFlights.GetElements("A_Flight"))
            {
                box.OutboundFlights.Add(ParseRecommendationFlight(of));
            }
        }

        var returnFlights = rb.GetElement("ReturnFlights");
        if (returnFlights != null)
        {
            foreach (var inf in returnFlights.GetElements("A_Flight"))
            {
                box.InboundFlights.Add(ParseRecommendationFlight(inf));
            }
        }

        // OtherFlights: MP (Multi-city) aramalarda 3. ve sonraki bacak uçuşları
        var otherFlights = rb.GetElement("OtherFlights");
        if (otherFlights != null)
        {
            foreach (var of in otherFlights.GetElements("A_Flight"))
            {
                box.OtherFlights.Add(ParseRecommendationFlight(of));
            }
        }

        // BrandedFares: FlightOption ile aynı mantık — GetDescendants kullan
        // çünkü v2 yapısında BrandedFares > BrandedFareItems (wrapper) > BrandedFareItem şeklinde nested gelebilir
        var brandedFaresElement = rb.GetElement("BrandedFares")
            ?? rb.GetDescendants("BrandedFares").FirstOrDefault()
            ?? rb.GetDescendants("T_BrandedFare_v2").FirstOrDefault();
        if (brandedFaresElement != null)
        {
            // GetDescendants: hem doğrudan child hem de BrandedFareItems wrapper içindeki öğeleri yakalar
            foreach (var bfi in brandedFaresElement.GetDescendants("BrandedFareItem"))
            {
                box.BrandedFareItems.Add(ParseBrandedFareItem(bfi));
            }

            // BrandedItem'ları (isim + kurallar) BrandId ile doğru BrandedFareItem'a eşleştir
            foreach (var bi in brandedFaresElement.GetDescendants("BrandedItem"))
            {
                var brandedItem = ParseBrandedItem(bi);
                var matchedFareItem = box.BrandedFareItems.FirstOrDefault(bfi =>
                    bfi.BrandedFarePassengers.Any(p =>
                        p.FareComponents.Any(fc => fc.BrandId == brandedItem.BrandId)));
                if (matchedFareItem != null)
                {
                    matchedFareItem.BrandedItems.Add(brandedItem);
                }
                else
                {
                    // Eşleşme bulunamadıysa tüm BrandedFareItem'lara ekle (fallback)
                    foreach (var bfi in box.BrandedFareItems)
                        bfi.BrandedItems.Add(brandedItem);
                }
            }
        }

        // SubOptionFlightIds: DepartureFlights + ReturnFlights + OtherFlights altındaki tüm FlightId'leri topla
        box.SubOptionFlightIds = box.OutboundFlights
            .Where(f => Guid.TryParse(f.FlightId, out _))
            .Select(f => Guid.Parse(f.FlightId!))
            .Concat(box.InboundFlights
                .Where(f => Guid.TryParse(f.FlightId, out _))
                .Select(f => Guid.Parse(f.FlightId!)))
            .Concat(box.OtherFlights
                .Where(f => Guid.TryParse(f.FlightId, out _))
                .Select(f => Guid.Parse(f.FlightId!)))
            .ToList();

        return box;
    }

    private static RecommendationFlight ParseRecommendationFlight(XElement flight)
    {
        var rf = new RecommendationFlight
        {
            FlightId = flight.GetValue("FlightId"),
            Duration = flight.GetIntValue("Duration")
        };

        foreach (var seg in flight.GetDescendants("A_FlightSegment"))
        {
            rf.Segments.Add(ParseSegment(seg));
        }

        return rf;
    }

    #region Allocate

    private async Task<AllocateResponse> AllocateAsync(
        string sessionId,
        string sessionToken,
        AllocateRequest request)
    {
        var soapRequest = BuildAllocateSoapRequest(sessionId, sessionToken, request);

        try
        {
            _logger.LogInformation("[Allocate] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Shopping/Allocate");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Allocate] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[Allocate] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new AllocateResponse
                {
                    HasError = true,
                    ErrorMessage = $"Allocate HTTP {(int)response.StatusCode}: {responseText}",
                    RawSoapResponse = responseText
                };
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception xmlEx)
            {
                _logger.LogError(xmlEx, "[Allocate] XML parse hatasi. Response XML degil. Ilk 500 karakter: {ResponseStart}",
                    responseText.Length > 500 ? responseText[..500] : responseText);
                return new AllocateResponse
                {
                    HasError = true,
                    ErrorMessage = $"Allocate: Servis yaniti XML olarak parse edilemedi. Hata: {xmlEx.Message}",
                    RawSoapResponse = responseText
                };
            }

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                return new AllocateResponse
                {
                    HasError = true,
                    ErrorMessage = doc.GetValue("ErrorMessage") ?? doc.GetValue("Message") ?? doc.GetValue("ServiceError"),
                    RawSoapResponse = responseText
                };
            }

            var result = ParseAllocateResponse(doc);
            result.RawSoapResponse = responseText;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Allocate] Exception");
            return new AllocateResponse
            {
                HasError = true,
                ErrorMessage = $"Allocate hatasi: {ex.Message}"
            };
        }
    }

    private static string BuildAllocateSoapRequest(
        string sessionId,
        string sessionToken,
        AllocateRequest request)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:arr=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"">
<soapenv:Header/>
<soapenv:Body xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"">
   <tem:Allocate>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{sessionId}</trev:SessionId>
            <trev:SessionToken>{sessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev1:Form>
             <trev1:SelectedItems>
                <trev1:IO_AllocationItem>{(!string.IsNullOrEmpty(request.BrandedFareItemId) ? $@"
                   <trev1:BrandedFareItemId>{request.BrandedFareItemId}</trev1:BrandedFareItemId>" : @"
                   <trev1:BrandedFareItemId i:nil=""true""/>")}
                   <trev1:ProductId>{request.ProductId}</trev1:ProductId>
                   <trev1:SelectedServiceFee>
                      <trev1:Amount>{request.SelectedServiceFee.ToString(System.Globalization.CultureInfo.InvariantCulture)}</trev1:Amount>
                      <trev1:ProductItemServiceFee i:nil=""true""/>
                   </trev1:SelectedServiceFee>
                   {(request.SubOptions != null && request.SubOptions.Count > 0
                    ? $@"<trev1:SubOptions>{string.Join("", request.SubOptions.Select(g => $@"
                      <arr:guid>{g:D}</arr:guid>"))}
                   </trev1:SubOptions>"
                    : @"<trev1:SubOptions i:nil=""true""/>")}
                </trev1:IO_AllocationItem>
             </trev1:SelectedItems>
          </trev1:Form>
      </tem:request>
   </tem:Allocate>
</soapenv:Body>
</soapenv:Envelope>";
    }

    private static AllocateResponse ParseAllocateResponse(XDocument doc)
    {
        var response = new AllocateResponse
        {
            HasError = false
        };

        // Debug: tum element isimlerini topla
        var allElements = doc.Descendants().Select(x => x.Name.LocalName).Distinct().ToList();
        response.DebugInfo = $"Elements found: {string.Join(", ", allElements)}";

        // ShoppingFile bilgileri
        var shoppingFile = doc.GetDescendants("ShoppingFile").FirstOrDefault();
        if (shoppingFile != null)
        {
            response.ShoppingFileId = shoppingFile.GetValue("Id");
            response.IsPriceChanged = shoppingFile.GetBoolValue("IsPriceChanged");
            response.IsFlightInfoChanged = string.IsNullOrEmpty(shoppingFile.GetValue("IsFlightInfoChanged"))
                ? null
                : shoppingFile.GetBoolValue("IsFlightInfoChanged");
            response.Currency = shoppingFile.GetValue("Currency");
            response.CanBeReserved = shoppingFile.GetBoolValue("CanBeReserved");
            response.IsCreditCardPaymentEnabled = shoppingFile.GetBoolValue("Is_CC_Payment_Enabled");
            response.IsRunningAccountPaymentEnabled = shoppingFile.GetBoolValue("Is_RA_Payment_Enabled");
            response.MaxServiceCommission = shoppingFile.GetDecimalValue("MaxSc");
            response.MinServiceCommission = shoppingFile.GetDecimalValue("MinSc");

            // CustomerInfo
            var customerInfo = shoppingFile.GetDescendants("CustomerInfo").FirstOrDefault();
            if (customerInfo != null)
            {
                response.CustomerInfo = new AllocateCustomerInfo
                {
                    BusinessId = customerInfo.GetValue("BusinessId"),
                    BusinessName = customerInfo.GetValue("BusinessName"),
                    Email = customerInfo.GetValue("Email"),
                    Username = customerInfo.GetValue("Username")
                };
            }

            // AirBookings
            foreach (var ab in shoppingFile.GetDescendants("T_AirBooking"))
            {
                response.AirBookings.Add(ParseAllocateAirBooking(ab));
            }

            // PriceSummary
            var priceSummary = shoppingFile.GetDescendants("PriceSummary").FirstOrDefault();
            if (priceSummary != null)
            {
                response.PriceSummary = new AllocatePriceSummary
                {
                    GrandTotal = priceSummary.GetDecimalValue("GrandTotal"),
                    TotalBaseFare = priceSummary.GetDecimalValue("TotalBaseFare"),
                    TotalTaxes = priceSummary.GetDecimalValue("TotalTaxes"),
                    TotalServiceFee = priceSummary.GetDecimalValue("TotalServiceFee"),
                    Currency = response.Currency // PriceSummary icinde Currency yok, parent'tan al
                };

                // T_PriceItem'lari parse et
                foreach (var pi in priceSummary.GetDescendants("T_PriceItem"))
                {
                    var priceItem = new AllocatePriceItem
                    {
                        ProductId = pi.GetValue("ProductId"),
                        ProductType = pi.GetValue("ProductType"),
                        Total = pi.GetDecimalValue("Total")
                    };
                    response.PriceSummary.PriceItems.Add(priceItem);
                }

                // GrandTotal 0 ise T_PriceItem toplamini kullan
                if (response.PriceSummary.GrandTotal == 0 && response.PriceSummary.PriceItems.Count > 0)
                {
                    response.PriceSummary.GrandTotal = response.PriceSummary.PriceItems.Sum(x => x.Total);
                }
            }
        }

        // Passengers (T_Passenger) � TempTag degerlerini parse et
        // BookingItems'tan PaxReferenceId'leri topla (TempTag ile eslestirmek icin)
        var paxRefLookup = response.AirBookings
            .SelectMany(ab => ab.BookingItems)
            .Where(bi => bi.PaxReferenceId != null)
            .ToDictionary(bi => bi.PaxSequenceNo, bi => bi.PaxReferenceId);

        foreach (var pax in doc.GetDescendants("T_Passenger"))
        {
            var seqNo = pax.GetIntValue("SequenceNo");
            paxRefLookup.TryGetValue(seqNo, out var paxRefId);

            var rawTempTag = pax.GetValue("TempTag");
            response.Passengers.Add(new AllocatePassenger
            {
                // TempTag bos gelebilir; bu durumda PaxReferenceId kullanilmali
                TempTag = !string.IsNullOrEmpty(rawTempTag) ? rawTempTag : paxRefId,
                SequenceNo = seqNo,
                Type = pax.GetValue("Type"),
                PaxReferenceId = paxRefId
            });
        }

        // T_Passenger bulunamadiysa BookingItems'tan yolcu bilgilerini turet
        if (response.Passengers.Count == 0)
        {
            var allBookingItems = response.AirBookings.SelectMany(ab => ab.BookingItems).ToList();
            foreach (var item in allBookingItems)
            {
                // TempTag olarak PaxReferenceId kullanilmali � yoksa BiletBank API eslestirme yapamiyor
                response.Passengers.Add(new AllocatePassenger
                {
                    TempTag = item.PaxReferenceId ?? Guid.NewGuid().ToString(),
                    SequenceNo = item.PaxSequenceNo,
                    Type = item.PaxType ?? "ADT",
                    PaxReferenceId = item.PaxReferenceId
                });
            }
        }

        // LastAllocatedProductIds
        var productIds = doc.GetDescendants("LastAllocatedProductIds").FirstOrDefault();
        if (productIds != null)
        {
            foreach (var guid in productIds.Elements())
            {
                if (!string.IsNullOrEmpty(guid.Value))
                    response.LastAllocatedProductIds.Add(guid.Value);
            }
        }

        return response;
    }

    private static AllocateAirBooking ParseAllocateAirBooking(XElement ab)
    {
        var booking = new AllocateAirBooking
        {
            ProductId = ab.GetValue("ProductId"),
            PNR = ab.GetValue("BookingCode"),
            ProviderId = ab.GetValue("ProviderId"),
            Status = ab.GetValue("Status"),
            Currency = ab.GetValue("Currency"),
            TotalFare = ab.GetDecimalValue("TotalFare"),
            BaseFare = ab.GetDecimalValue("BaseFare"),
            Taxes = ab.GetDecimalValue("Taxes"),
            NetFare = ab.GetDecimalValue("NetFare"),
            ServiceFee = ab.GetDecimalValue("ServiceFee"),
            LastSellerCommission = ab.GetDecimalValue("LastSellerCommission"),
            IsRefundable = ab.GetBoolValue("IsRefundable"),
            ValidatingCarrier = ab.GetValue("ValidatingCarrier"),
            FlightType = ab.GetValue("FlightType")
        };

        // CanBeReserved - FlightRuleAttribute icinde
        var ruleAttr = ab.GetDescendants("FlightRuleAttribute").FirstOrDefault();
        if (ruleAttr != null)
        {
            booking.CanBeReserved = ruleAttr.GetBoolValue("IsReservable");
        }

        // BookingItems (T_AirBookingItem) - yolcu bazli fiyat detayi
        foreach (var bi in ab.GetDescendants("T_AirBookingItem"))
        {
            var bookingItem = new AllocateBookingItem
            {
                ProductItemId = bi.GetValue("ProductItemId"),
                Currency = bi.GetValue("Currency"),
                BaseFare = bi.GetDecimalValue("BaseFare"),
                Taxes = bi.GetDecimalValue("Taxes"),
                TotalFare = bi.GetDecimalValue("TotalFare"),
                NetFare = bi.GetDecimalValue("NetFare"),
                ServiceFee = bi.GetDecimalValue("ServiceFee"),
                SystemServiceFee = bi.GetDecimalValue("SystemServiceFee"),
                Baggage = bi.GetValue("Baggage")
            };

            // PaxReference
            var paxRef = bi.GetDescendants("PaxReference").FirstOrDefault();
            if (paxRef != null)
            {
                bookingItem.PaxType = paxRef.GetValue("LocalPaxType");
                bookingItem.PaxSequenceNo = paxRef.GetIntValue("LocalSequenceNo");
                bookingItem.PaxReferenceId = paxRef.GetValue("PaxReferenceId");
            }

            booking.BookingItems.Add(bookingItem);
        }

        // Segments
        foreach (var seg in ab.GetDescendants("T_Segment"))
        {
            booking.Segments.Add(new AllocateSegment
            {
                SegmentId = seg.GetValue("Id"),
                OriginCode = seg.GetValue("OriginCode"),
                DestinationCode = seg.GetValue("DestinationCode"),
                DepartureDay = seg.GetValue("DepartureDay"),
                DepartureTime = seg.GetValue("DepartureTime"),
                ArrivalDay = seg.GetValue("ArrivalDay"),
                ArrivalTime = seg.GetValue("ArrivalTime"),
                MarketingAirline = seg.GetValue("MarketingAirline"),
                OperatingAirline = seg.GetValue("OperatingAirline"),
                FlightNumber = seg.GetValue("FlightNumber"),
                BookingClass = seg.GetValue("BookingClass"),
                FareBasis = seg.GetValue("FareBasis"),
                Duration = seg.GetValue("Duration"),
                SelectedBrandedFareItemId = seg.GetValue("SelectedBrandedFareItemId"),
                SequenceNo = seg.GetIntValue("SequenceNo")
            });
        }

        // BrandedFares
        var brandedFares = ab.GetDescendants("BrandedFares").FirstOrDefault();
        if (brandedFares != null)
        {
            // BrandedFareItem'lar (fiyat paketleri)
            foreach (var bfi in brandedFares.GetDescendants("BrandedFareItem"))
            {
                var fareItem = new AllocateBrandedFareItem
                {
                    BrandedFareItemId = bfi.GetValue("BrandedFareItemId"),
                    Currency = bfi.GetValue("Currency")
                };

                var totalInfo = bfi.GetDescendants("TotalFareInfo").FirstOrDefault();
                if (totalInfo != null)
                {
                    fareItem.TotalFare = totalInfo.GetDecimalValue("TotalFare");
                    fareItem.TotalTaxes = totalInfo.GetDecimalValue("TotalTaxes");
                }

                foreach (var bfp in bfi.GetDescendants("BrandedFarePassenger"))
                {
                    var passenger = new AllocateBrandedFarePassenger
                    {
                        PassengerType = bfp.GetValue("PassengerType"),
                        PassengerCount = bfp.GetIntValue("PassengerCount")
                    };

                    var fareInfo = bfp.GetDescendants("PassengerFareInfo").FirstOrDefault();
                    if (fareInfo != null)
                    {
                        passenger.BaseFare = fareInfo.GetDecimalValue("BaseFare");
                        passenger.Taxes = fareInfo.GetDecimalValue("Taxes");
                        passenger.TotalFare = fareInfo.GetDecimalValue("TotalFare");
                        passenger.Currency = fareInfo.GetValue("Currency");
                    }

                    var fc = bfp.GetDescendants("FareComponent").FirstOrDefault();
                    if (fc != null)
                    {
                        passenger.BookingClass = fc.GetValue("BookingClass");
                        passenger.CabinClass = fc.GetValue("CabinClass");
                        passenger.FareBasisCode = fc.GetValue("FareBasisCode");
                        passenger.BrandId = fc.GetValue("BrandId");
                        passenger.SeatsAvailable = fc.GetIntValue("SeatsAvailable");
                    }

                    fareItem.Passengers.Add(passenger);
                }

                booking.BrandedFareItems.Add(fareItem);
            }

            // BrandedItem'lar (paket aciklamalari: ECO, FLEX, PREMIUM)
            foreach (var bi in brandedFares.GetDescendants("BrandedItem"))
            {
                var brandedItem = new AllocateBrandedItem
                {
                    BrandId = bi.GetValue("BrandId"),
                    BrandCode = bi.GetValue("BrandCode"),
                    BrandName = bi.GetValue("BrandName")
                };

                foreach (var rule in bi.GetDescendants("BrandedRule"))
                {
                    brandedItem.Rules.Add(new AllocateBrandedRule
                    {
                        Application = rule.GetValue("Application"),
                        DisplayType = rule.GetValue("DisplayType"),
                        RuleDescription = rule.GetValue("RuleDescription"),
                        ServiceGroup = rule.GetValue("ServiceGroup")
                    });
                }

                booking.BrandedItems.Add(brandedItem);
            }

            // FreeBaggageAllowances
            foreach (var fba in brandedFares.GetDescendants("FreeBaggageAllowance"))
            {
                var paxBaggage = fba.GetDescendants("PaxFBA").FirstOrDefault();
                var paxType = fba.GetDescendants("PaxBaggageAllowance").FirstOrDefault();

                booking.BaggageAllowances.Add(new AllocateBaggageAllowance
                {
                    Id = fba.GetValue("Id"),
                    Allowance = paxBaggage?.GetValue("Allowance"),
                    Category = paxBaggage?.GetValue("Category"),
                    Type = paxBaggage?.GetValue("Type"),
                    Unit = paxBaggage?.GetValue("Unit"),
                    PaxType = paxType?.GetValue("PaxType")
                });
            }
        }

        return booking;
    }

    #endregion

    #region Booking

    private async Task<UpdatePassengersResponse> UpdatePassengersInternalAsync(
            string sessionId,
            string sessionToken,
            UpdatePassengersRequest request)
    {
        string debugXml = string.Empty;
        string responseText = string.Empty;

        try
        {
            var soapRequest = BuildUpdatePassengersSoapRequest(sessionId, sessionToken, request);
            debugXml = soapRequest;
            _logger.LogInformation("[UpdatePassengers] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Shopping/UpdatePassengers");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[UpdatePassengers] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[UpdatePassengers] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdatePassengersResponse
                {
                    HasError = true,
                    ErrorMessage = $"UpdatePassengers HTTP {(int)response.StatusCode}: {responseText}",
                    RawSoapResponse = responseText,
                    RawSoapRequest = debugXml
                };
            }

            // Bos response kontrolu — BiletBank bazen bos yanit donebiliyor
            if (string.IsNullOrWhiteSpace(responseText))
            {
                _logger.LogError("[UpdatePassengers] BiletBank bos response dondu. HTTP Status: {StatusCode}", (int)response.StatusCode);
                return new UpdatePassengersResponse
                {
                    HasError = true,
                    ErrorMessage = "UpdatePassengers: BiletBank bos yanit dondu. Lutfen tekrar deneyin.",
                    RawSoapResponse = responseText,
                    RawSoapRequest = debugXml
                };
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception xmlEx)
            {
                _logger.LogError(xmlEx, "[UpdatePassengers] XML parse hatasi. Response XML degil. Ilk 500 karakter: {ResponseStart}",
                    responseText.Length > 500 ? responseText[..500] : responseText);
                return new UpdatePassengersResponse
                {
                    HasError = true,
                    ErrorMessage = $"UpdatePassengers: Servis yaniti XML olarak parse edilemedi. Hata: {xmlEx.Message}",
                    RawSoapResponse = responseText,
                    RawSoapRequest = debugXml
                };
            }

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                var errorMsg = doc.GetValue("ErrorMessage")
                    ?? doc.GetValue("DebugMessage")
                    ?? doc.GetValue("Message")
                    ?? doc.GetValue("ServiceError");

                // BiletBank'in tam hata detayini logla — debug icin kritik
                var debugMsg = doc.GetValue("DebugMessage");
                var serviceName = doc.GetValue("Name");
                _logger.LogError(
                    "[UpdatePassengers] BiletBank HATA dondu. ErrorMessage={ErrorMessage}, DebugMessage={DebugMessage}, ServiceName={ServiceName}",
                    errorMsg, debugMsg, serviceName);

                return new UpdatePassengersResponse
                {
                    HasError = true,
                    ErrorMessage = errorMsg,
                    RawSoapResponse = responseText,
                    RawSoapRequest = debugXml
                };
            }

            return new UpdatePassengersResponse { HasError = false, RawSoapResponse = responseText, RawSoapRequest = debugXml };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdatePassengers] Exception");
            return new UpdatePassengersResponse
            {
                HasError = true,
                ErrorMessage = $"UpdatePassengers exception: {ex.Message} | StackTrace: {ex.StackTrace}",
                RawSoapResponse = responseText,
                RawSoapRequest = debugXml
            };
        }
    }

    private static string BuildUpdatePassengersSoapRequest(
        string sessionId,
        string sessionToken,
        UpdatePassengersRequest request)
    {
        var passengersXml = new StringBuilder();

        for (int i = 0; i < request.Passengers.Count; i++)
        {
            var pax = request.Passengers[i];
            var isContact = i == 0;

            var birthDate = pax.BirthDate.Contains('T')
                ? pax.BirthDate.Split('T')[0]
                : pax.BirthDate;

            // Kural 1: TempTag = Allocate response'taki PaxReferenceId (aynen kopyalanacak)
            // BookFlightAsync'te TempTag = PaxReferenceId olarak set ediliyor,
            // burada fallback olarak PaxReferenceId'ye bakilir
            var tempTag = !string.IsNullOrEmpty(pax.TempTag)
                ? pax.TempTag
                : !string.IsNullOrEmpty(pax.PaxReferenceId)
                    ? pax.PaxReferenceId
                    : throw new InvalidOperationException(
                        $"Yolcu {i + 1}: TempTag ve PaxReferenceId bos. Allocate response'taki paxReferenceId degerini gonderin.");
            // Kural 2: Id = her zaman yeni GUID uretilecek
            var paxId = Guid.NewGuid().ToString();

            // Telefon numarasini BiletBank formatina (+CC-XXXXXXXXXX) cevir
            var phoneNumber = isContact ? FormatPhoneForBiletBank(request.Contact.Phone) : "";

            // Yurt ici ucuslarda TCKN doluysa PassportNo/PassportCountry gonderilmemeli,
            // yurt disi ucuslarda PassportNo doluysa CitizenNo gonderilmemeli.
            var hasCitizenNo = !string.IsNullOrWhiteSpace(pax.CitizenNo);
            var hasPassportNo = !string.IsNullOrWhiteSpace(pax.PassportNo);

            var citizenNoValue = hasCitizenNo ? pax.CitizenNo! : (hasPassportNo ? "" : "");
            var passportNoValue = hasPassportNo ? pax.PassportNo! : (hasCitizenNo ? "" : "");
            var passportCountryValue = hasPassportNo ? (pax.PassportCountry ?? pax.Nationality) : (hasCitizenNo ? "" : "");

            // PassportValidDate: yurt disi ucuslarda pasaport gecerlilik tarihi
            var passportValidDateValue = hasPassportNo && !string.IsNullOrWhiteSpace(pax.PassportExpiry)
                ? (pax.PassportExpiry!.Contains('T') ? pax.PassportExpiry.Split('T')[0] : pax.PassportExpiry)
                : null;

            // BiletBank dokumantasyonundaki element sirasi:
            // BirthDate, CitizenNo, Email, FirstName, Gender, Id, IfContact, LastName,
            // Nationality, PassportCountry, PassportNo, PassportValidDate, PaxReferences, Phone, SequenceNo, TempTag, Type, WheelChairServiceType
            var safeFirstName = SecurityElement.Escape(pax.FirstName) ?? "";
            var safeLastName = SecurityElement.Escape(pax.LastName) ?? "";
            var safeEmail = isContact ? (SecurityElement.Escape(request.Contact.Email) ?? "") : "";
            var safeCitizenNo = SecurityElement.Escape(citizenNoValue) ?? "";
            var safePassportNo = SecurityElement.Escape(passportNoValue) ?? "";
            var safePassportCountry = SecurityElement.Escape(passportCountryValue) ?? "";
            var safeNationality = SecurityElement.Escape(pax.Nationality) ?? "";

            // PaxReferences: nil olarak gonderilir — BiletBank yolcu-urun eslestirmesini
            // Type ve SequenceNo uzerinden otomatik yapar. Tek bir ProductItemId ile
            // tum yolculari eslestirmek yanlis sonuc verir (CHD/INF farkli ProductItemId'ye sahiptir).
            passengersXml.Append($@"
            <trev2:T_Passenger>
              <trev2:BirthDate>{birthDate}</trev2:BirthDate>
              <trev2:CitizenNo>{safeCitizenNo}</trev2:CitizenNo>
              <trev2:Email>{safeEmail}</trev2:Email>
              <trev2:FirstName>{safeFirstName}</trev2:FirstName>
              <trev2:Gender>{pax.Gender}</trev2:Gender>
              <trev2:Id>{paxId}</trev2:Id>
              <trev2:IfContact>{isContact.ToString().ToLowerInvariant()}</trev2:IfContact>
              <trev2:LastName>{safeLastName}</trev2:LastName>
              <trev2:Nationality>{safeNationality}</trev2:Nationality>
              {(string.IsNullOrEmpty(safePassportCountry) ? "<trev2:PassportCountry i:nil=\"true\"/>" : $"<trev2:PassportCountry>{safePassportCountry}</trev2:PassportCountry>")}
              {(string.IsNullOrEmpty(safePassportNo) ? "<trev2:PassportNo i:nil=\"true\"/>" : $"<trev2:PassportNo>{safePassportNo}</trev2:PassportNo>")}
              {(passportValidDateValue != null ? $"<trev2:PassportValidDate>{passportValidDateValue}</trev2:PassportValidDate>" : "<trev2:PassportValidDate i:nil=\"true\"/>")}
              <trev2:PaxReferences i:nil=""true""/>
              <trev2:Phone>{phoneNumber}</trev2:Phone>
              <trev2:SequenceNo>{pax.SequenceNo}</trev2:SequenceNo>
              <trev2:TempTag>{tempTag}</trev2:TempTag>
              <trev2:Type>{pax.PaxType}</trev2:Type>
              <trev2:WheelChairServiceType>0</trev2:WheelChairServiceType>
            </trev2:T_Passenger>");
        }

        // WSDL IO_UpdatePassengersForm order: IsContactRefused, KvkkConfirmation,
        // ModifiedPassengers, NewPassengers, ProductIds
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:trev2=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Shopping""
xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""
xmlns:arr=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"">
<soap:Body>
   <tem:UpdatePassengers>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{sessionId}</trev:SessionId>
            <trev:SessionToken>{sessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev1:Form>
            <trev1:IsContactRefused i:nil=""true""/>
            <trev1:KvkkConfirmation>false</trev1:KvkkConfirmation>
            <trev1:ModifiedPassengers i:nil=""true""/>
            <trev1:NewPassengers>{passengersXml}
            </trev1:NewPassengers>
            <trev1:ProductIds>
               <arr:guid>{request.ProductId}</arr:guid>
            </trev1:ProductIds>
         </trev1:Form>
      </tem:request>
   </tem:UpdatePassengers>
</soap:Body>
</soap:Envelope>";
    }

    #endregion

    #region MakePreBooking

    public async Task<MakePreBookingResponse> MakePreBookingAsync(MakePreBookingRequest request)
    {
        _logger.LogInformation(
            "[MakePreBooking] Parametreler: SessionId={SessionId}, ProductId={ProductId}, BrandedFareItemId={BrandedFareItemId}, ShoppingFileId={ShoppingFileId}",
            request.SessionId, request.ProductId, request.BrandedFareItemId ?? "(null)", request.ShoppingFileId);

        var soapRequest = BuildMakePreBookingSoapRequest(
            request.SessionId,
            request.SessionToken,
            request.ProductId,
            request.BrandedFareItemId,
            request.ShoppingFileId);

        _logger.LogInformation("[MakePreBooking] SOAP Request:\n{SoapRequest}", soapRequest);

        var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Shopping/MakePrebooking");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var response = await _httpClient.PostAsync(_proxyUrl, content, cts.Token);
            var responseText = await response.Content.ReadAsStringAsync(cts.Token);

            _logger.LogInformation("[MakePreBooking] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[MakePreBooking] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new MakePreBookingResponse
                {
                    HasError = true,
                    ErrorMessage = $"MakePreBooking HTTP {(int)response.StatusCode}: {responseText}"
                };
            }

            // Bos response kontrolu — BiletBank bazen bos yanit donebiliyor
            if (string.IsNullOrWhiteSpace(responseText))
            {
                _logger.LogError("[MakePreBooking] BiletBank bos response dondu. HTTP Status: {StatusCode}", (int)response.StatusCode);
                return new MakePreBookingResponse
                {
                    HasError = true,
                    ErrorMessage = "MakePreBooking: BiletBank bos yanit dondu. Lutfen tekrar deneyin."
                };
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception xmlEx)
            {
                _logger.LogError(xmlEx, "[MakePreBooking] XML parse hatasi. Response XML degil. Ilk 500 karakter: {ResponseStart}",
                    responseText.Length > 500 ? responseText[..500] : responseText);
                return new MakePreBookingResponse
                {
                    HasError = true,
                    ErrorMessage = $"MakePreBooking: Servis yaniti XML olarak parse edilemedi. Hata: {xmlEx.Message}"
                };
            }

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                var errorMsg = doc.GetValue("ErrorMessage")
                    ?? doc.GetValue("DebugMessage")
                    ?? doc.GetValue("Message")
                    ?? doc.GetValue("ServiceError");

                // BiletBank'in tam hata detayini logla — debug icin kritik
                var debugMsg = doc.GetValue("DebugMessage");
                var serviceName = doc.GetValue("Name");
                _logger.LogError(
                    "[MakePreBooking] BiletBank HATA dondu.\n  ErrorMessage={ErrorMessage}\n  DebugMessage={DebugMessage}\n  ServiceName={ServiceName}\n  ProductId={ProductId}\n  BrandedFareItemId={BrandedFareItemId}\n  ShoppingFileId={ShoppingFileId}\nSOAP Request:\n{SoapRequest}\nSOAP Response:\n{SoapResponse}",
                    errorMsg, debugMsg, serviceName,
                    request.ProductId, request.BrandedFareItemId ?? "(null)", request.ShoppingFileId,
                    soapRequest, responseText);

                return new MakePreBookingResponse
                {
                    HasError = true,
                    ErrorMessage = errorMsg
                };
            }

            return ParseMakePreBookingResponse(doc);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[MakePreBooking] Timeout � BiletBank 90 saniye icinde yanit vermedi.");
            return new MakePreBookingResponse
            {
                HasError = true,
                ErrorMessage = "MakePreBooking zaman asimina ugradi. BiletBank API yanitlamadi. Lutfen tekrar deneyin."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MakePreBooking] Exception");
            return new MakePreBookingResponse
            {
                HasError = true,
                ErrorMessage = $"MakePreBooking hatasi: {ex.Message}"
            };
        }
    }

    private static string BuildMakePreBookingSoapRequest(
        string sessionId,
        string sessionToken,
        string productId,
        string brandedFareItemId,
        string shoppingFileId)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:trev2=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Air"">
<soap:Body>
   <tem:MakePrebooking>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{sessionId}</trev:SessionId>
            <trev:SessionToken>{sessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev:ExtraParamList>
            <trev:ExtendedData>
               <trev:Name>IntendedShoppingFileId</trev:Name>
               <trev:Value>{shoppingFileId}</trev:Value>
            </trev:ExtendedData>
            <trev:ExtendedData>
               <trev:Name>DoReservation</trev:Name>
               <trev:Value>true</trev:Value>
            </trev:ExtendedData>
         </trev:ExtraParamList>
          <trev1:Form>{(!string.IsNullOrEmpty(brandedFareItemId) ? $@"
             <trev1:Branded>
                <trev1:IO_Air_Branded_Form>
                   <trev1:BrandedFareItemId>{brandedFareItemId}</trev1:BrandedFareItemId>
                   <trev1:ProductId>{productId}</trev1:ProductId>
                </trev1:IO_Air_Branded_Form>
             </trev1:Branded>" : "")}
             <trev1:ProductIds xmlns:arr=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"">
                <arr:guid>{productId}</arr:guid>
             </trev1:ProductIds>
          </trev1:Form>
      </tem:request>
   </tem:MakePrebooking>
</soap:Body>
</soap:Envelope>";
    }

    private static MakePreBookingResponse ParseMakePreBookingResponse(XDocument doc)
    {
        var response = new MakePreBookingResponse { HasError = false };

        var shoppingFile = doc.GetDescendants("ShoppingFile").FirstOrDefault();
        if (shoppingFile != null)
        {
            response.ShoppingFileId = shoppingFile.GetValue("Id");
            response.IsPriceChanged = shoppingFile.GetBoolValue("IsPriceChanged");
            response.IsFlightInfoChanged = shoppingFile.GetBoolValue("IsFlightInfoChanged");
            response.IsReservationCancelled = shoppingFile.GetBoolValue("IsReservationCancelled");
            response.RemainingSum = shoppingFile.GetDecimalValue("RemainingSum");
            response.Currency = shoppingFile.GetValue("Currency");
            response.CanBeReserved = shoppingFile.GetBoolValue("CanBeReserved");
        }

        // AirBooking bilgileri — round-trip icin birden fazla T_AirBooking olabilir
        // Ilk T_AirBooking'den BookingCode, ProductId, Status alinir
        // Fiyatlar tum T_AirBooking'lerden toplanir (gidis + donus = toplam)
        var allAirBookings = doc.GetDescendants("T_AirBooking").ToList();
        var firstAirBooking = allAirBookings.FirstOrDefault();
        if (firstAirBooking != null)
        {
            response.BookingCode = firstAirBooking.GetValue("BookingCode");
            response.ProductId = firstAirBooking.GetValue("ProductId");
            response.Status = firstAirBooking.GetValue("Status");

            // Fiyatlari tum T_AirBooking'lerden topla
            response.BaseFare = allAirBookings.Sum(ab => ab.GetDecimalValue("BaseFare"));
            response.Taxes = allAirBookings.Sum(ab => ab.GetDecimalValue("Taxes"));
            response.ServiceFee = allAirBookings.Sum(ab => ab.GetDecimalValue("ServiceFee"));
            response.TotalFare = allAirBookings.Sum(ab => ab.GetDecimalValue("TotalFare"));

            var ruleAttr = firstAirBooking.GetDescendants("FlightRuleAttribute").FirstOrDefault();
            if (ruleAttr != null)
                response.CanBeReserved = ruleAttr.GetBoolValue("IsReservable");
        }

        // TimeTable � on rezervasyon ve rezervasyon gecerlilik sureleri
        var timeTable = doc.GetDescendants("TimeTable").FirstOrDefault();
        if (timeTable != null)
        {
            var prebookingRaw = timeTable.GetValue("Prebooking_ExpiresAt");
            if (DateTime.TryParse(prebookingRaw, out var prebookingExpiry))
                response.PrebookingExpiresAt = prebookingExpiry;

            var reservationRaw = timeTable.GetValue("Reservation_ExpiresAt");
            if (DateTime.TryParse(reservationRaw, out var reservationExpiry))
                response.ReservationExpiresAt = reservationExpiry;
        }

        // Yolcular
        foreach (var pax in doc.GetDescendants("T_Passenger"))
        {
            response.Passengers.Add(new PreBookingPassenger
            {
                FirstName = pax.GetValue("FirstName"),
                LastName = pax.GetValue("LastName"),
                Type = pax.GetValue("Type"),
                CitizenNo = pax.GetValue("CitizenNo"),
                Gender = pax.GetValue("Gender")
            });
        }

        // Segmentler — tum T_AirBooking'lerden topla (round-trip icin 2 ayri booking olabilir)
        int segSequence = 0;
        foreach (var ab in allAirBookings)
        {
            foreach (var seg in ab.GetDescendants("T_Segment"))
            {
                segSequence++;
                response.Segments.Add(new PreBookingSegment
                {
                    SegmentId = seg.GetValue("Id"),
                    OriginCode = seg.GetValue("OriginCode"),
                    DestinationCode = seg.GetValue("DestinationCode"),
                    DepartureDay = FormatDay(seg.GetValue("DepartureDay")),
                    DepartureTime = FormatIso8601DurationAsTime(seg.GetValue("DepartureTime")),
                    ArrivalDay = FormatDay(seg.GetValue("ArrivalDay")),
                    ArrivalTime = FormatIso8601DurationAsTime(seg.GetValue("ArrivalTime")),
                    FlightNumber = seg.GetValue("FlightNumber"),
                    MarketingAirline = seg.GetValue("MarketingAirline"),
                    BookingClass = seg.GetValue("BookingClass")
                });
            }
        }

        return response;
    }

    #endregion

    #region RemoveProduct

    public async Task<RemoveProductResponse> RemoveProductAsync(RemoveProductRequest request)
    {
        // GUID validasyonu � bos GUID gonderimini engelle
        if (!Guid.TryParse(request.ProductId, out var productGuid) || productGuid == Guid.Empty)
        {
            _logger.LogWarning("[RemoveProduct] ProductId bos veya gecersiz GUID: '{ProductId}'", request.ProductId);
            return new RemoveProductResponse
            {
                HasError = true,
                ErrorMessage = $"ProductId gecerli bir GUID olmali. Gelen deger: '{request.ProductId}'"
            };
        }

        if (!Guid.TryParse(request.ShoppingFileId, out var shoppingGuid) || shoppingGuid == Guid.Empty)
        {
            _logger.LogWarning("[RemoveProduct] ShoppingFileId bos veya gecersiz GUID: '{ShoppingFileId}'", request.ShoppingFileId);
            return new RemoveProductResponse
            {
                HasError = true,
                ErrorMessage = $"ShoppingFileId gecerli bir GUID olmali. Gelen deger: '{request.ShoppingFileId}'"
            };
        }

        var shoppingFileId = request.ShoppingFileId ?? "";

        var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:arr=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"">
<soap:Body>
   <tem:RemoveProduct>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{request.SessionId}</trev:SessionId>
            <trev:SessionToken>{request.SessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev:ExtraParamList>
            <trev:ExtendedData>
               <trev:Name>IntendedShoppingFileId</trev:Name>
               <trev:Value>{shoppingFileId}</trev:Value>
            </trev:ExtendedData>
         </trev:ExtraParamList>
         <trev1:Form>
            <trev1:ProductId>{request.ProductId}</trev1:ProductId>
            <trev1:ShoppingFileId>{shoppingFileId}</trev1:ShoppingFileId>
         </trev1:Form>
      </tem:request>
   </tem:RemoveProduct>
</soap:Body>
</soap:Envelope>";

        try
        {
            _logger.LogInformation("[RemoveProduct] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Shopping/RemoveProduct");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[RemoveProduct] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[RemoveProduct] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new RemoveProductResponse
                {
                    HasError = true,
                    ErrorMessage = $"RemoveProduct HTTP {(int)response.StatusCode}: {responseText}"
                };
            }

            var doc = XDocument.Parse(responseText);
            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                return new RemoveProductResponse
                {
                    HasError = true,
                    ErrorMessage = doc.GetValue("ErrorMessage") ?? doc.GetValue("Message") ?? doc.GetValue("ServiceError")
                };
            }

            var shoppingFile = doc.GetDescendants("ShoppingFile").FirstOrDefault();
            return new RemoveProductResponse
            {
                HasError = false,
                ShoppingFileId = shoppingFile?.GetValue("Id")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RemoveProduct] Exception");
            return new RemoveProductResponse
            {
                HasError = true,
                ErrorMessage = $"RemoveProduct hatasi: {ex.Message}"
            };
        }
    }

    #endregion

    #region MakePayment

    public async Task<MakePaymentResponse> MakePaymentAsync(MakePaymentRequest request)
    {
        try
        {
            var amount = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture); var currency = request.Currency ?? "TRY";
            var sessionId = request.SessionId ?? "";
            var sessionToken = request.SessionToken ?? "";
            var shoppingFileId = request.ShoppingFileId ?? "";

            // Temel validasyonlar � BiletBank'a gondermeden once kontrol et
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(sessionToken))
            {
                return new MakePaymentResponse
                {
                    HasError = true,
                    ErrorMessage = "SessionId ve SessionToken bos olamaz."
                };
            }

            if (string.IsNullOrWhiteSpace(shoppingFileId))
            {
                return new MakePaymentResponse
                {
                    HasError = true,
                    ErrorMessage = "ShoppingFileId bos olamaz. MakePreBooking adiminda alinan ShoppingFileId degerini gonderin."
                };
            }

            if (request.Amount <= 0)
            {
                return new MakePaymentResponse
                {
                    HasError = true,
                    ErrorMessage = "Amount sifirdan buyuk olmalidir."
                };
            }

            string soapRequest;
            string soapAction;

            if ((request.PaymentType == "CreditCard" || request.PaymentType == "CreditCardDirect") && request.CreditCard != null)
            {
                // CreditCard null kontrolu
                if (string.IsNullOrWhiteSpace(request.CreditCard.CardNumber) ||
                    string.IsNullOrWhiteSpace(request.CreditCard.CardHolderName) ||
                    string.IsNullOrWhiteSpace(request.CreditCard.ExpiryMonth) ||
                    string.IsNullOrWhiteSpace(request.CreditCard.ExpiryYear) ||
                    string.IsNullOrWhiteSpace(request.CreditCard.Cvv))
                {
                    return new MakePaymentResponse
                    {
                        HasError = true,
                        ErrorMessage = "Kredi karti bilgileri eksik: CardNumber, CardHolderName, ExpiryMonth, ExpiryYear ve Cvv alanlari zorunludur."
                    };
                }



                // XML'de ozel karakterleri escape et
                var cardHolder = SecurityElement.Escape(request.CreditCard?.CardHolderName ?? "");
                // Kart numarasindan bosluk, tire ve diger ozel karakterleri temizle
                var cardNumber = new string((request.CreditCard?.CardNumber ?? "").Where(char.IsDigit).ToArray());
                var cardCvv = new string((request.CreditCard?.Cvv ?? "").Where(char.IsDigit).ToArray());

                // BiletBank ExpirationMonth/ExpirationYear int olarak bekler
                // Frontend "01" veya "2026" gibi string gonderebilir
                var rawMonth = request.CreditCard?.ExpiryMonth ?? "0";
                var rawYear = request.CreditCard?.ExpiryYear ?? "0";
                var cardExpMonth = int.TryParse(rawMonth, out var expM) ? expM.ToString() : "0";
                var cardExpYear = int.TryParse(rawYear, out var expY)
      ? (expY >= 100 ? (expY % 100).ToString() : expY.ToString())
      : "0";

                // Debug: Kart bilgilerini maskeli olarak logla
                var maskedCard = cardNumber.Length >= 4
                    ? $"{cardNumber[..6]}****{cardNumber[^4..]}"
                    : "KISA";
                _logger.LogInformation(
                    "[MakePayment] Kart bilgileri: Holder={CardHolder}, Number={MaskedCard} (len={CardLen}), ExpMonth={ExpMonth}, ExpYear={ExpYear}, CVV_len={CvvLen}",
                    cardHolder, maskedCard, cardNumber.Length, cardExpMonth, cardExpYear, cardCvv.Length);

                var installmentXml = !string.IsNullOrWhiteSpace(request.InstallmentOptionId)
                    ? $"<trev1:InstallmentOptionId>{request.InstallmentOptionId}</trev1:InstallmentOptionId>"
                    : "";

                var isPartial = request.IsPartialPayment.ToString().ToLowerInvariant();
                var deductCommission = request.DeductLastSellerCommission.ToString().ToLowerInvariant();

                // BiletBank test ortami 3D'siz odemeye izin vermiyor (WithoutThreeDIsNotAuthorized)
                // Bu nedenle CreditCard ve CreditCardDirect her ikisi de Init3DPayment uzerinden gider
                var use3D = true;

                if (use3D)
                {
                    soapAction = "http://tempuri.org/I_Shopping/MakePayment_Init3DPayment";
var callbackBase = request.ContinueUrl ?? "http://37.148.212.253:5000/api/Flight/3d-callback";
var continueUrl = SecurityElement.Escape($"{callbackBase}?sfid={request.ShoppingFileId}");
                    soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"">
<soap:Body>
   <tem:MakePayment_Init3DPayment>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{sessionId}</trev:SessionId>
            <trev:SessionToken>{sessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev:ExtraParamList>
            <trev:ExtendedData>
               <trev:Name>IntendedShoppingFileId</trev:Name>
               <trev:Value>{shoppingFileId}</trev:Value>
            </trev:ExtendedData>
         </trev:ExtraParamList>
         <trev1:DeductLastSellerCommission>{deductCommission}</trev1:DeductLastSellerCommission>
 <trev1:Form>
   <trev1:Amount>{amount}</trev1:Amount>
   <trev1:BillingName>{cardHolder}</trev1:BillingName>
   <trev1:CV2>{cardCvv}</trev1:CV2>
   <trev1:CardHolder>{cardHolder}</trev1:CardHolder>
   <trev1:CardNumber>{cardNumber}</trev1:CardNumber>
   <trev1:CardType/>
   <trev1:Currency>{currency}</trev1:Currency>
   <trev1:ExpirationMonth>{cardExpMonth}</trev1:ExpirationMonth>
   <trev1:ExpirationYear>{cardExpYear}</trev1:ExpirationYear>
   {installmentXml}
   <trev1:OriginalAmount>{amount}</trev1:OriginalAmount>
   <trev1:ReturnUrl>{continueUrl}</trev1:ReturnUrl>
   <trev1:ShoppingFileId>{shoppingFileId}</trev1:ShoppingFileId>
</trev1:Form>
      </tem:request>
   </tem:MakePayment_Init3DPayment>
</soap:Body>
</soap:Envelope>";
                }
                else
                {
                    // Non-3D dogrudan kredi karti odemesi
                    soapAction = "http://tempuri.org/I_Shopping/MakePayment_FromCreditCard";

                    soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"">
<soap:Body>
   <tem:MakePayment_FromCreditCard>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{sessionId}</trev:SessionId>
            <trev:SessionToken>{sessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev:ExtraParamList>
            <trev:ExtendedData>
               <trev:Name>IntendedShoppingFileId</trev:Name>
               <trev:Value>{shoppingFileId}</trev:Value>
            </trev:ExtendedData>
         </trev:ExtraParamList>
         <trev1:DeductLastSellerCommission>{deductCommission}</trev1:DeductLastSellerCommission>
         <trev1:PreAuthForm>
            <trev1:Amount>{amount}</trev1:Amount>
            <trev1:CV2>{cardCvv}</trev1:CV2>
            <trev1:CardHolder>{cardHolder}</trev1:CardHolder>
            <trev1:CardNumber>{cardNumber}</trev1:CardNumber>
            <trev1:Currency>{currency}</trev1:Currency>
            <trev1:ExpirationMonth>{cardExpMonth}</trev1:ExpirationMonth>
            <trev1:ExpirationYear>{cardExpYear}</trev1:ExpirationYear>
            {installmentXml}
            <trev1:OriginalAmount>{amount}</trev1:OriginalAmount>
            <trev1:ShoppingFileId>{shoppingFileId}</trev1:ShoppingFileId>
         </trev1:PreAuthForm>
      </tem:request>
   </tem:MakePayment_FromCreditCard>
</soap:Body>
</soap:Envelope>";
                }
            }
            else
            {
                // RunningAccount (cari hesap) odemesi
                soapAction = "http://tempuri.org/I_Shopping/MakePayment_FromRunningAccount";

                var raDeductCommission = request.DeductLastSellerCommission.ToString().ToLowerInvariant();

                soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"">
<soap:Body>
   <tem:MakePayment_FromRunningAccount>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{sessionId}</trev:SessionId>
            <trev:SessionToken>{sessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev:ExtraParamList>
            <trev:ExtendedData>
               <trev:Name>IntendedShoppingFileId</trev:Name>
               <trev:Value>{shoppingFileId}</trev:Value>
            </trev:ExtendedData>
         </trev:ExtraParamList>
         <trev1:DeductLastSellerCommission>{raDeductCommission}</trev1:DeductLastSellerCommission>
         <trev1:PaymentForm>
            <trev1:Amount>{amount}</trev1:Amount>
            <trev1:Currency>{currency}</trev1:Currency>
            <trev1:IsPartialPayment>false</trev1:IsPartialPayment>
            <trev1:PaymentType>RA_BALANCE_PAYMENT</trev1:PaymentType>
            <trev1:ShoppingFileId>{shoppingFileId}</trev1:ShoppingFileId>
         </trev1:PaymentForm>
      </tem:request>
   </tem:MakePayment_FromRunningAccount>
</soap:Body>
</soap:Envelope>";
            }

            // Kart bilgilerini loglamadan sadece islem bilgisini logla
            _logger.LogInformation("[MakePayment] PaymentType={PaymentType}, Amount={Amount}, Currency={Currency}, ShoppingFileId={ShoppingFileId}",
                request.PaymentType, amount, currency, shoppingFileId);

            // SOAP request'i logla — debug icin kritik
            _logger.LogInformation("[MakePayment] SOAP Request:\n{SoapRequest}", soapRequest);
            _logger.LogInformation("[MakePayment] SOAPAction: {SoapAction}", soapAction);

            // Dosyaya yaz — sunucuda debug icin
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, "payment-debug.log");
                var logEntry = $"""
=== MakePayment {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} ===
PaymentType: {request.PaymentType}
Amount: {amount}
Currency: {currency}
ShoppingFileId: {shoppingFileId}
SOAPAction: {soapAction}

--- SOAP REQUEST ---
{soapRequest}
""";
                File.AppendAllText(logFile, logEntry);
            }
            catch { /* log yazma hatasi kritik degil */ }

            // BiletBank test ortami bazen UnknownSystemError donuyor � retry mekanizmasi
            const int maxRetries = 2;
            string? responseText = null;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var content = CreateSoapContent(soapRequest, soapAction);

                var response = await _httpClient.PostAsync(_proxyUrl, content, cts.Token);
                responseText = await response.Content.ReadAsStringAsync(cts.Token);

                _logger.LogInformation("[MakePayment] Attempt {Attempt}/{MaxRetries} � HTTP Status: {StatusCode}, Response Length: {Length}",
                    attempt, maxRetries, (int)response.StatusCode, responseText?.Length ?? 0);

                if (!response.IsSuccessStatusCode)
                {
                    return new MakePaymentResponse
                    {
                        HasError = true,
                        ErrorMessage = $"MakePayment HTTP {(int)response.StatusCode}: {responseText}",
                        RawSoapRequest = soapRequest,
                        RawSoapResponse = responseText
                    };
                }

                if (string.IsNullOrWhiteSpace(responseText))
                {
                    return new MakePaymentResponse
                    {
                        HasError = true,
                        ErrorMessage = "MakePayment: Bos response alindi.",
                        RawSoapRequest = soapRequest
                    };
                }

                // BiletBank UnknownSystemError + IsSystem:true donduyse retry yap
                if (attempt < maxRetries
                    && responseText.Contains("UnknownSystemError")
                    && responseText.Contains("<IsSystem>true</IsSystem>"))
                {
                    _logger.LogWarning("[MakePayment] BiletBank UnknownSystemError (IsSystem). {Delay}ms sonra tekrar deneniyor... (Attempt {Attempt}/{MaxRetries})",
                        2000, attempt, maxRetries);
                    await Task.Delay(2000);
                    continue;
                }

                break; // Basarili veya farkli hata � donguyu kir
            }

            _logger.LogInformation("[MakePayment] SOAP Response:\n{SoapResponse}", responseText);

            // Response'u dosyaya yaz — sunucuda debug icin
            try 
            {
                var logFile = Path.Combine(AppContext.BaseDirectory, "logs", "payment-debug.log");
                var responseLog = $"""

--- SOAP RESPONSE ---
{responseText}
=== END ===

""";
                File.AppendAllText(logFile, responseLog);
            }
            catch { /* log yazma hatasi kritik degil */ }

            // Init3DPayment response'u dogrudan HTML (3D Secure redirect sayfasi) donebilir
            var trimmed = responseText.TrimStart();
            if (trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[MakePayment] 3D Secure HTML response algilandi (dogrudan HTML).");
                return new MakePaymentResponse
                {
                    HasError = false,
                    IsPaymentSuccessful = false,
                    Is3DSecureRequired = true,
                    ThreeDSecureUrl = null,
                    Status = "Awaiting3DSecure",
                    ThreeDSecureHtml = responseText,
                    RawSoapRequest = soapRequest,
                    RawSoapResponse = responseText
                };
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception parseEx)
            {
                // XML parse edilemedi � HTML form olabilir, 3D Secure icerigi olarak dondur
                _logger.LogWarning(parseEx, "[MakePayment] XML parse hatasi. Response muhtemelen 3D Secure HTML icerigi.");
                return new MakePaymentResponse
                {
                    HasError = false,
                    IsPaymentSuccessful = false,
                    Is3DSecureRequired = true,
                    Status = "Awaiting3DSecure",
                    ThreeDSecureHtml = responseText,
                    RawSoapRequest = soapRequest,
                    RawSoapResponse = responseText
                };
            }

            var hasErrorVal = doc.GetValue("HasError");
            if (hasErrorVal == "true")
            {
                // ServiceError altindaki hata bilgilerini topla
                var serviceError = doc.GetDescendants("ServiceError").FirstOrDefault();
                var errMsg = serviceError?.GetValue("ErrorMessage")
                    ?? doc.GetValue("ErrorMessage")
                    ?? serviceError?.GetValue("DebugMessage")
                    ?? doc.GetValue("DebugMessage")
                    ?? doc.GetValue("Message");
                var debugMsg = serviceError?.GetValue("DebugMessage");
                var errorName = serviceError?.GetValue("Name");

                var fullError = errMsg ?? "Bilinmeyen hata";
                if (!string.IsNullOrEmpty(debugMsg) && debugMsg != errMsg)
                    fullError += $" | Debug: {debugMsg}";
                if (!string.IsNullOrEmpty(errorName))
                    fullError += $" | Hata tipi: {errorName}";

                return new MakePaymentResponse
                {
                    HasError = true,
                    ErrorMessage = fullError,
                    RawSoapRequest = soapRequest,
                    RawSoapResponse = responseText
                };
            }

            var shoppingFileEl = doc.GetDescendants("ShoppingFile").FirstOrDefault();
            var paymentId = doc.GetValue("PaymentId");

            // T_AirBooking'den booking durumunu ve PNR'i al
            var airBookingEl = doc.GetDescendants("T_AirBooking").FirstOrDefault();
            var bookingStatus = airBookingEl?.GetValue("Status");
            var bookingCode = airBookingEl?.GetValue("BookingCode");

            // PriceSummary'den GrandTotal
            var priceSummary = shoppingFileEl?.GetDescendants("PriceSummary").FirstOrDefault();
            var grandTotal = priceSummary?.GetDecimalValue("GrandTotal") ?? 0;

            // RunningAccountStatus � cari hesap bakiyesi
            var raStatus = doc.GetDescendants("RunningAccountStatus").FirstOrDefault();
            var raBalance = raStatus?.GetDecimalValue("Balance");

            // RemainingSum
            var remainingSum = shoppingFileEl != null ? shoppingFileEl.GetDecimalValue("RemainingSum") : 0;

            // 3D Secure � BiletBank farkli alanlarda donebilir
            var threeDUrl = doc.GetValue("ContinueUrl")
                ?? doc.GetValue("RedirectUrl")
                ?? doc.GetValue("ThreeDSecureUrl")
                ?? doc.GetValue("PaymentUrl")
                ?? doc.GetValue("ACSUrl");

            // 3D HTML content � XML icinde CDATA veya element value olarak gelebilir
            var threeDHtml = doc.GetValue("ThreeDHtml")
                ?? doc.GetValue("HtmlContent")
                ?? doc.GetValue("PaymentHtml")
                ?? doc.GetValue("HTMLContent")
                ?? doc.GetValue("PaymentPageContent");

            // Bazi durumlarda 3D HTML icerigi derin bir elementin altinda olabilir
            if (string.IsNullOrEmpty(threeDHtml))
            {
                var htmlElement = doc.Descendants()
                    .FirstOrDefault(x => !x.HasElements
                        && !string.IsNullOrEmpty(x.Value)
                        && (x.Value.Contains("<form", StringComparison.OrdinalIgnoreCase)
                            || x.Value.Contains("<FORM", StringComparison.OrdinalIgnoreCase)));
                if (htmlElement != null)
                {
                    threeDHtml = htmlElement.Value;
                    _logger.LogInformation("[MakePayment] 3D Secure HTML '{ElementName}' elementinde bulundu.",
                        htmlElement.Name.LocalName);
                }
            }

            var is3DRequired = !string.IsNullOrEmpty(threeDUrl) || !string.IsNullOrEmpty(threeDHtml);

            // PaymentId empty GUID ise odeme basarisiz/beklemede
            var isPaymentPending = paymentId == "00000000-0000-0000-0000-000000000000";

            // Odeme basari kontrolu:
            // - HasError=false (zaten yukarida kontrol edildi)
            // - PaymentId gecerli bir GUID (bos GUID degil)
            // - 3D Secure gerekmiyor
            // NOT: RemainingSum, RA odemede odeme SONRASI bile > 0 gelebilir (BiletBank'in yapisi).
            //      Asil gosterge PaymentId'nin gecerli olmasi ve HasError=false olmasidir.
            var isSuccessful = !is3DRequired && !isPaymentPending;

            if (is3DRequired)
            {
                _logger.LogInformation("[MakePayment] 3D Secure algilandi. URL={ThreeDUrl}, HTML uzunluk={HtmlLen}",
                    threeDUrl, threeDHtml?.Length ?? 0);
            }

            _logger.LogInformation(
                "[MakePayment] Sonuc: PaymentId={PaymentId}, BookingStatus={BookingStatus}, PNR={PNR}, RemainingSum={RemainingSum}, GrandTotal={GrandTotal}, RABalance={RABalance}",
                paymentId, bookingStatus, bookingCode, remainingSum, grandTotal, raBalance);

            // Taksit seceneklerini parse et
            var installmentOptions = new List<PaymentInstallmentOption>();
            var paymentOptions = shoppingFileEl?.GetDescendants("T_PaymentInstallmentOption");
            if (paymentOptions != null)
            {
                foreach (var opt in paymentOptions)
                {
                    installmentOptions.Add(new PaymentInstallmentOption
                    {
                        InstallmentOptionId = opt.GetValue("InstallmentOptionId"),
                        BankName = opt.GetValue("BankName"),
                        Program = opt.GetValue("Program"),
                        InstallmentCount = opt.GetIntValue("InstallmentCount"),
                        TotalInstallmentCount = opt.GetIntValue("TotalInstallmentCount"),
                        BonusInstallmentCount = opt.GetIntValue("BonusInstallmentCount"),
                        MonthlyPayment = opt.GetDecimalValue("MontlyPayment"),
                        SubTotal = opt.GetDecimalValue("SubTotal"),
                        AmountOfInterest = opt.GetDecimalValue("AmountOfInterest"),
                        RateOfInterest = opt.GetDecimalValue("RateOfInterest"),
                        Currency = opt.GetValue("Currency")
                    });
                }
            }

            // Status belirleme:
            // 3D gerekiyorsa � Awaiting3DSecure
            // PaymentId bos GUID ise � PaymentPending
            // Basarili ise � T_AirBooking.Status (Reservation vb.) veya "Paid"
            string resolvedStatus;
            if (is3DRequired)
                resolvedStatus = "Awaiting3DSecure";
            else if (isPaymentPending)
                resolvedStatus = "PaymentPending";
            else
                resolvedStatus = bookingStatus ?? "Paid";

            return new MakePaymentResponse
            {
                HasError = false,
                IsPaymentSuccessful = isSuccessful,
                Status = resolvedStatus,
                ShoppingFileId = shoppingFileEl?.GetValue("Id"),
                RemainingSum = remainingSum,
                Currency = shoppingFileEl?.GetValue("Currency") ?? currency,
                PaymentReferenceId = paymentId,
                PNR = bookingCode,
                BookingStatus = bookingStatus,
                RunningAccountBalance = raBalance,
                GrandTotal = grandTotal,
                ThreeDSecureUrl = threeDUrl,
                Is3DSecureRequired = is3DRequired,
                ThreeDSecureHtml = threeDHtml,
                InstallmentOptions = installmentOptions,
                RawSoapRequest = soapRequest,
                RawSoapResponse = responseText
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[MakePayment] Timeout � BiletBank 90 saniye icinde yanit vermedi.");
            return new MakePaymentResponse
            {
                HasError = true,
                ErrorMessage = "MakePayment zaman asimina ugradi. BiletBank API yanitlamadi. Lutfen tekrar deneyin."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MakePayment] Exception. StackTrace: {StackTrace}", ex.StackTrace);
            return new MakePaymentResponse
            {
                HasError = true,
                ErrorMessage = $"MakePayment hatasi: {ex.Message} | Konum: {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"
            };
        }
    }

    #endregion 

    #region Complete3DPayment

    public async Task<MakePaymentResponse> Complete3DPaymentAsync(Complete3DPaymentRequest request)
    {
        try
        {
            var sessionId = request.SessionId ?? "";
            var sessionToken = request.SessionToken ?? "";
            var shoppingFileId = request.ShoppingFileId ?? "";

            // Bankadan gelen parametreleri ExtraParamList olarak olustur
            // IntendedShoppingFileId her zaman eklenmeli
            var extraParams = new StringBuilder();
            extraParams.Append($@"
            <trev:ExtendedData>
               <trev:Name>IntendedShoppingFileId</trev:Name>
               <trev:Value>{shoppingFileId}</trev:Value>
            </trev:ExtendedData>");
            foreach (var kvp in request.BankResponseParameters)
            {
                extraParams.Append($@"
            <trev:ExtendedData>
               <trev:Name>{SecurityElement.Escape(kvp.Key)}</trev:Name>
               <trev:Value>{SecurityElement.Escape(kvp.Value)}</trev:Value>
            </trev:ExtendedData>");
            }

            var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"">
<soap:Body>
   <tem:MakePayment_Complete3DPayment>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{sessionId}</trev:SessionId>
            <trev:SessionToken>{sessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev:ExtraParamList>{extraParams}
         </trev:ExtraParamList>
         <trev1:PaymentForm>
            <trev1:ShoppingFileId>{shoppingFileId}</trev1:ShoppingFileId>
         </trev1:PaymentForm>
      </tem:request>
   </tem:MakePayment_Complete3DPayment>
</soap:Body>
</soap:Envelope>";

            _logger.LogInformation("[Complete3DPayment] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Shopping/MakePayment_Complete3DPayment");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Complete3DPayment] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[Complete3DPayment] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new MakePaymentResponse
                {
                    HasError = true,
                    ErrorMessage = $"Complete3DPayment HTTP {(int)response.StatusCode}: {responseText}"
                };
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception xmlEx)
            {
                _logger.LogError(xmlEx, "[Complete3DPayment] XML parse hatasi. Response XML degil.");
                return new MakePaymentResponse
                {
                    HasError = true,
                    ErrorMessage = $"Complete3DPayment: Servis yaniti XML olarak parse edilemedi. Hata: {xmlEx.Message}"
                };
            }

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                return new MakePaymentResponse
                {
                    HasError = true,
                    ErrorMessage = doc.GetValue("ErrorMessage")
                        ?? doc.GetValue("DebugMessage")
                        ?? doc.GetValue("Message")
                        ?? doc.GetValue("ServiceError")
                };
            }

            var shoppingFileEl = doc.GetDescendants("ShoppingFile").FirstOrDefault();
            var paymentId = doc.GetValue("PaymentId");
            var remainingSum = shoppingFileEl?.GetDecimalValue("RemainingSum") ?? 0;

            var airBookingEl = doc.GetDescendants("T_AirBooking").FirstOrDefault();
            var bookingStatus = airBookingEl?.GetValue("Status");
            var bookingCode = airBookingEl?.GetValue("BookingCode");

            var priceSummary = shoppingFileEl?.GetDescendants("PriceSummary").FirstOrDefault();
            var grandTotal = priceSummary?.GetDecimalValue("GrandTotal") ?? 0;

            var isPaymentPending = paymentId == "00000000-0000-0000-0000-000000000000";
            var isSuccessful = !isPaymentPending;

            _logger.LogInformation(
                "[Complete3DPayment] Sonuc: PaymentId={PaymentId}, BookingStatus={BookingStatus}, PNR={PNR}, RemainingSum={RemainingSum}",
                paymentId, bookingStatus, bookingCode, remainingSum);

            return new MakePaymentResponse
            {
                HasError = false,
                IsPaymentSuccessful = isSuccessful,
                Status = isSuccessful ? (bookingStatus ?? "Paid") : "PaymentFailed",
                ShoppingFileId = shoppingFileEl?.GetValue("Id"),
                RemainingSum = remainingSum,
                Currency = shoppingFileEl?.GetValue("Currency") ?? "TRY",
                PaymentReferenceId = paymentId,
                PNR = bookingCode,
                BookingStatus = bookingStatus,
                GrandTotal = grandTotal,
                Is3DSecureRequired = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Complete3DPayment] Exception");
            return new MakePaymentResponse
            {
                HasError = true,
                ErrorMessage = $"Complete3DPayment hatasi: {ex.Message}"
            };
        }
    }

    #endregion

    #region FinalizeShopping

    public async Task<FinalizeShoppingResponse> FinalizeShoppingAsync(FinalizeShoppingRequest request)
    {
        try
        {
            var sessionId = request.SessionId ?? "";
            var sessionToken = request.SessionToken ?? "";
            var shoppingFileId = request.ShoppingFileId ?? "";
            var productId = request.ProductId ?? "";

            // BillingInfo — null ise varsayilan degerler kullanilir
            var billing = request.BillingInfo ?? new ShoppingBillingInfo();
            var billingXml = $@"<trev1:BillingInfo>
               <trev2:Address_City>{SecurityElement.Escape(billing.AddressCity)}</trev2:Address_City>
               <trev2:Address_Detail>{SecurityElement.Escape(billing.AddressDetail)}</trev2:Address_Detail>
               <trev2:Address_District>{SecurityElement.Escape(billing.AddressDistrict)}</trev2:Address_District>
               <trev2:Address_ZipCode>{SecurityElement.Escape(billing.AddressZipCode)}</trev2:Address_ZipCode>
               <trev2:BillingName>{SecurityElement.Escape(billing.BillingName)}</trev2:BillingName>
               <trev2:CountryCode>{SecurityElement.Escape(billing.CountryCode)}</trev2:CountryCode>
               <trev2:IfCompany>{billing.IfCompany}</trev2:IfCompany>
               <trev2:TaxNo>{SecurityElement.Escape(billing.TaxNo)}</trev2:TaxNo>
               <trev2:TaxOffice>{SecurityElement.Escape(billing.TaxOffice)}</trev2:TaxOffice>
            </trev1:BillingInfo>";

            var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:trev2=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Shopping""
xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"">
<soap:Body>
   <tem:FinalizeShopping>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{sessionId}</trev:SessionId>
            <trev:SessionToken>{sessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev:ExtraParamList>
            <trev:ExtendedData>
               <trev:Name>IntendedShoppingFileId</trev:Name>
               <trev:Value>{shoppingFileId}</trev:Value>
            </trev:ExtendedData>
         </trev:ExtraParamList>
         <trev1:Form>
            {billingXml}
            <trev1:ShoppingFileId>{shoppingFileId}</trev1:ShoppingFileId>
         </trev1:Form>
      </tem:request>
   </tem:FinalizeShopping>
</soap:Body>
</soap:Envelope>";
            _logger.LogInformation("[FinalizeShopping] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Shopping/FinalizeShopping");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[FinalizeShopping] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[FinalizeShopping] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new FinalizeShoppingResponse
                {
                    HasError = true,
                    ErrorMessage = $"FinalizeShopping HTTP {(int)response.StatusCode}: {responseText}",
                    RawSoapRequest = soapRequest,
                    RawSoapResponse = responseText
                };
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception xmlEx)
            {
                _logger.LogError(xmlEx, "[FinalizeShopping] XML parse hatasi. Response XML degil.");
                return new FinalizeShoppingResponse
                {
                    HasError = true,
                    ErrorMessage = $"FinalizeShopping: Servis yaniti XML olarak parse edilemedi. Hata: {xmlEx.Message}",
                    RawSoapRequest = soapRequest,
                    RawSoapResponse = responseText
                };
            }

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                var serviceError = doc.GetDescendants("ServiceError").FirstOrDefault();
                var errMsg = serviceError?.GetValue("ErrorMessage")
                    ?? doc.GetValue("ErrorMessage")
                    ?? doc.GetValue("Message");
                return new FinalizeShoppingResponse
                {
                    HasError = true,
                    ErrorMessage = errMsg,
                    RawSoapRequest = soapRequest,
                    RawSoapResponse = responseText
                };
            }

            var result = ParseFinalizeShoppingResponse(doc);
            result.RawSoapRequest = soapRequest;
            result.RawSoapResponse = responseText;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FinalizeShopping] Exception");
            return new FinalizeShoppingResponse
            {
                HasError = true,
                ErrorMessage = $"FinalizeShopping hatasi: {ex.Message}"
            };
        }
    }

    private static FinalizeShoppingResponse ParseFinalizeShoppingResponse(XDocument doc)
    {
        var result = new FinalizeShoppingResponse { HasError = false };

        var shoppingFile = doc.GetDescendants("ShoppingFile").FirstOrDefault();
        if (shoppingFile != null)
        {
            result.ShoppingFileId = shoppingFile.GetValue("Id");
            result.Currency = shoppingFile.GetValue("Currency");
        }

        // AirBooking bilgileri — round-trip icin birden fazla T_AirBooking olabilir
        var allAirBookings = doc.GetDescendants("T_AirBooking").ToList();
        var firstAirBooking = allAirBookings.FirstOrDefault();
        if (firstAirBooking != null)
        {
            result.BookingCode = firstAirBooking.GetValue("BookingCode");
            result.Status = firstAirBooking.GetValue("Status");
            result.TotalFare = allAirBookings.Sum(ab => ab.GetDecimalValue("TotalFare"));
        }

        // E-bilet numaralarini topla
        // 1. Once T_AirBookingItem'lardaki TicketNumber'i kontrol et
        var bookingItems = doc.GetDescendants("T_AirBookingItem").ToList();
        var passengers = doc.GetDescendants("T_Passenger").ToList();

        foreach (var item in bookingItems)
        {
            var ticketNo = item.GetValue("TicketNumber");
            if (string.IsNullOrEmpty(ticketNo)) continue;

            // PaxReference uzerinden yolcu bilgisini bul
            var paxRef = item.GetDescendants("PaxReference").FirstOrDefault();
            var passengerId = paxRef?.GetValue("PassengerId");

            var matchedPax = passengers.FirstOrDefault(p => p.GetValue("Id") == passengerId);

            result.Tickets.Add(new TicketInfo
            {
                FirstName = matchedPax?.GetValue("FirstName"),
                LastName = matchedPax?.GetValue("LastName"),
                PaxType = matchedPax?.GetValue("Type") ?? paxRef?.GetValue("LocalPaxType"),
                TicketNumber = ticketNo,
                SequenceNo = paxRef?.GetIntValue("LocalSequenceNo") ?? 0
            });
        }

        // 2. Eger BookingItem'dan ticket bulunamadiysa T_Passenger'dan dene
        if (result.Tickets.Count == 0)
        {
            int seqNo = 0;
            foreach (var pax in passengers)
            {
                seqNo++;
                var ticketNo = pax.GetValue("TicketNumber");
                if (!string.IsNullOrEmpty(ticketNo))
                {
                    result.Tickets.Add(new TicketInfo
                    {
                        FirstName = pax.GetValue("FirstName"),
                        LastName = pax.GetValue("LastName"),
                        PaxType = pax.GetValue("Type"),
                        TicketNumber = ticketNo,
                        SequenceNo = seqNo
                    });
                }
            }
        }

        // IsFinalized: status basarili bir durumu gosteriyorsa true
        var finalizedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Booking", "Ticketed", "Reservation" };
        result.IsFinalized = !string.IsNullOrEmpty(result.Status)
            && finalizedStatuses.Contains(result.Status);

        return result;
    }

    #endregion

    #region PokeShoppingFile

    public async Task<PokeShoppingFileResponse> PokeShoppingFileAsync(PokeShoppingFileRequest request)
    {
        var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping"">
<soap:Body>
   <tem:PokeShoppingFile>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{request.SessionId}</trev:SessionId>
            <trev:SessionToken>{request.SessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev1:Form>
            <trev1:ShoppingFileId>{request.ShoppingFileId}</trev1:ShoppingFileId>
         </trev1:Form>
      </tem:request>
   </tem:PokeShoppingFile>
</soap:Body>
</soap:Envelope>";

        try
        {
            _logger.LogInformation("[PokeShoppingFile] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Shopping/PokeShoppingFile");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[PokeShoppingFile] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[PokeShoppingFile] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new PokeShoppingFileResponse
                {
                    HasError = true,
                    ErrorMessage = $"PokeShoppingFile HTTP {(int)response.StatusCode}: {responseText}"
                };
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception xmlEx)
            {
                _logger.LogError(xmlEx, "[PokeShoppingFile] XML parse hatasi. Response XML degil.");
                return new PokeShoppingFileResponse
                {
                    HasError = true,
                    ErrorMessage = $"PokeShoppingFile: Servis yaniti XML olarak parse edilemedi. Hata: {xmlEx.Message}"
                };
            }

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                return new PokeShoppingFileResponse
                {
                    HasError = true,
                    ErrorMessage = doc.GetValue("ErrorMessage") ?? doc.GetValue("Message") ?? doc.GetValue("ServiceError")
                };
            }

            var shoppingFile = doc.GetDescendants("ShoppingFile").FirstOrDefault();
            var airBooking = doc.GetDescendants("T_AirBooking").FirstOrDefault();

            var priceSummary = shoppingFile?.GetDescendants("PriceSummary").FirstOrDefault();

            var pokeResult = new PokeShoppingFileResponse
            {
                HasError = false,
                ShoppingFileId = shoppingFile?.GetValue("Id"),
                Status = airBooking?.GetValue("Status"),
                IsPriceChanged = shoppingFile != null && shoppingFile.GetBoolValue("IsPriceChanged"),
                IsFlightInfoChanged = shoppingFile != null && shoppingFile.GetBoolValue("IsFlightInfoChanged"),
                RemainingSum = shoppingFile?.GetDecimalValue("RemainingSum") ?? 0,
                Currency = shoppingFile?.GetValue("Currency"),
                IsReservationCancelled = shoppingFile != null && shoppingFile.GetBoolValue("IsReservationCancelled"),
                BookingCode = airBooking?.GetValue("BookingCode"),
                GrandTotal = priceSummary?.GetDecimalValue("GrandTotal") ?? 0,
                RawSoapRequest = soapRequest,
                RawSoapResponse = responseText
            };

            // Ticket bilgilerini parse et
            foreach (var item in doc.GetDescendants("T_AirBookingItem"))
            {
                var ticketNo = item.GetValue("TicketNumber");
                if (string.IsNullOrEmpty(ticketNo)) continue;

                var paxRef = item.GetDescendants("PaxReference").FirstOrDefault();
                var passengerId = paxRef?.GetValue("PassengerId");
                var matchedPax = doc.GetDescendants("T_Passenger")
                    .FirstOrDefault(p => p.GetValue("Id") == passengerId);

                pokeResult.Tickets.Add(new TicketInfo
                {
                    FirstName = matchedPax?.GetValue("FirstName"),
                    LastName = matchedPax?.GetValue("LastName"),
                    PaxType = matchedPax?.GetValue("Type"),
                    TicketNumber = ticketNo,
                    SequenceNo = paxRef?.GetIntValue("LocalSequenceNo") ?? 0
                });
            }

            return pokeResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PokeShoppingFile] Exception");
            return new PokeShoppingFileResponse
            {
                HasError = true,
                ErrorMessage = $"PokeShoppingFile hatasi: {ex.Message}"
            };
        }
    }

    #endregion

    #region ReadShoppingFile

    public async Task<ReadShoppingFileResponse> ReadShoppingFileAsync(ReadShoppingFileRequest request)
    {
        try
        {
            var sessionId = request.SessionId ?? "";
            var sessionToken = request.SessionToken ?? "";
            var shoppingFileId = request.ShoppingFileId ?? "";

            var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping"">
<soap:Body>
   <tem:ReadShoppingFile>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{sessionId}</trev:SessionId>
            <trev:SessionToken>{sessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev1:ShoppingFileId>{shoppingFileId}</trev1:ShoppingFileId>
      </tem:request>
   </tem:ReadShoppingFile>
</soap:Body>
</soap:Envelope>";

            _logger.LogInformation("[ReadShoppingFile] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Shopping/ReadShoppingFile");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[ReadShoppingFile] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[ReadShoppingFile] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new ReadShoppingFileResponse
                {
                    HasError = true,
                    ErrorMessage = $"ReadShoppingFile HTTP {(int)response.StatusCode}: {responseText}"
                };
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception xmlEx)
            {
                _logger.LogError(xmlEx, "[ReadShoppingFile] XML parse hatasi. Response XML degil.");
                return new ReadShoppingFileResponse
                {
                    HasError = true,
                    ErrorMessage = $"ReadShoppingFile: Servis yaniti XML olarak parse edilemedi. Hata: {xmlEx.Message}"
                };
            }

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                return new ReadShoppingFileResponse
                {
                    HasError = true,
                    ErrorMessage = doc.GetValue("ErrorMessage") ?? doc.GetValue("Message") ?? doc.GetValue("ServiceError")
                };
            }

            return ParseReadShoppingFileResponse(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReadShoppingFile] Exception");
            return new ReadShoppingFileResponse
            {
                HasError = true,
                ErrorMessage = $"ReadShoppingFile hatasi: {ex.Message}"
            };
        }
    }

    private static ReadShoppingFileResponse ParseReadShoppingFileResponse(XDocument doc)
    {
        var result = new ReadShoppingFileResponse { HasError = false };

        var shoppingFile = doc.GetDescendants("ShoppingFile").FirstOrDefault();
        if (shoppingFile != null)
        {
            result.ShoppingFileId = shoppingFile.GetValue("Id");
            result.Currency = shoppingFile.GetValue("Currency");
            result.IsPriceChanged = shoppingFile.GetBoolValue("IsPriceChanged");
            result.IsReservationCancelled = shoppingFile.GetBoolValue("IsReservationCancelled");
            result.RemainingSum = shoppingFile.GetDecimalValue("RemainingSum");

            var priceSummary = shoppingFile.GetDescendants("PriceSummary").FirstOrDefault();
            if (priceSummary != null)
            {
                result.GrandTotal = priceSummary.GetDecimalValue("GrandTotal");
                result.BaseFare = priceSummary.GetDecimalValue("TotalBaseFare");
                result.Taxes = priceSummary.GetDecimalValue("TotalTaxes");
            }
        }

        // AirBooking bilgileri — round-trip icin birden fazla T_AirBooking olabilir
        var allAirBookings = doc.GetDescendants("T_AirBooking").ToList();
        var firstAirBooking = allAirBookings.FirstOrDefault();
        if (firstAirBooking != null)
        {
            result.BookingCode = firstAirBooking.GetValue("BookingCode");
            result.Status = firstAirBooking.GetValue("Status");
        }

        // Yolcular + bilet numaralari
        int seqNo = 0;
        foreach (var pax in doc.GetDescendants("T_Passenger"))
        {
            seqNo++;
            result.Passengers.Add(new ReadShoppingPassenger
            {
                FirstName = pax.GetValue("FirstName"),
                LastName = pax.GetValue("LastName"),
                Type = pax.GetValue("Type"),
                Gender = pax.GetValue("Gender"),
                CitizenNo = pax.GetValue("CitizenNo"),
                TicketNumber = pax.GetValue("TicketNumber"),
                SequenceNo = seqNo
            });

            var ticketNo = pax.GetValue("TicketNumber");
            if (!string.IsNullOrEmpty(ticketNo))
            {
                result.Tickets.Add(new TicketInfo
                {
                    FirstName = pax.GetValue("FirstName"),
                    LastName = pax.GetValue("LastName"),
                    PaxType = pax.GetValue("Type"),
                    TicketNumber = ticketNo,
                    SequenceNo = seqNo
                });
            }
        }

        // Segmentler — tum T_AirBooking'lerden topla (round-trip icin 2 ayri booking olabilir)
        int segSequence = 0;
        foreach (var ab in allAirBookings)
        {
            foreach (var seg in ab.GetDescendants("T_Segment"))
            {
                segSequence++;
                result.Segments.Add(new PreBookingSegment
                {
                    SegmentId = seg.GetValue("Id"),
                    OriginCode = seg.GetValue("OriginCode"),
                    DestinationCode = seg.GetValue("DestinationCode"),
                    DepartureDay = seg.GetValue("DepartureDay"),
                    DepartureTime = FormatIso8601DurationAsTime(seg.GetValue("DepartureTime")),
                    ArrivalDay = seg.GetValue("ArrivalDay"),
                    ArrivalTime = FormatIso8601DurationAsTime(seg.GetValue("ArrivalTime")),
                    FlightNumber = seg.GetValue("FlightNumber"),
                    MarketingAirline = seg.GetValue("MarketingAirline"),
                    BookingClass = seg.GetValue("BookingClass")
                });
            }
        }

        // Odemeler
        foreach (var payment in doc.GetDescendants("T_Payment"))
        {
            result.Payments.Add(new ReadShoppingPayment
            {
                PaymentType = payment.GetValue("PaymentType"),
                Amount = payment.GetDecimalValue("Amount"),
                Currency = payment.GetValue("Currency"),
                Status = payment.GetValue("Status"),
                ReferenceId = payment.GetValue("ReferenceId")
            });
        }

        return result;
    }

    #endregion

    #region Logout

    public async Task<LogoutResponse> LogoutAsync(LogoutRequest request)
    {
        var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base"">
<soap:Body>
   <tem:Logout>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{request.SessionId}</trev:SessionId>
            <trev:SessionToken>{request.SessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
      </tem:request>
   </tem:Logout>
</soap:Body>
</soap:Envelope>";

        try
        {
            _logger.LogInformation("[Logout] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = CreateSoapContent(soapRequest, "http://tempuri.org/I_Authentication/Logout");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Logout] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[Logout] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new LogoutResponse
                {
                    HasError = true,
                    ErrorMessage = $"Logout HTTP {(int)response.StatusCode}: {responseText}"
                };
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(responseText);
            }
            catch (Exception xmlEx)
            {
                _logger.LogError(xmlEx, "[Logout] XML parse hatasi. Response XML degil.");
                return new LogoutResponse
                {
                    HasError = true,
                    ErrorMessage = $"Logout: Servis yaniti XML olarak parse edilemedi. Hata: {xmlEx.Message}"
                };
            }

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                return new LogoutResponse
                {
                    HasError = true,
                    ErrorMessage = doc.GetValue("ErrorMessage") ?? doc.GetValue("Message") ?? doc.GetValue("ServiceError")
                };
            }

            return new LogoutResponse { HasError = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Logout] Exception");
            return new LogoutResponse
            {
                HasError = true,
                ErrorMessage = $"Logout hatasi: {ex.Message}"
            };
        }
    }

    #endregion

    /// <summary>
    /// Telefon numarasini BiletBank'in bekledi +CC-XXXXXXXXXX formatina cevirir.
    /// Bu metot SOAP XML olusturulmadan hemen once cagrilir � controller'dan
    /// ne gelirse gelsin burada garanti altina alinir.
    /// </summary>
    private static string FormatPhoneForBiletBank(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "+90-5000000000";

        // Zaten +CC-XXX formatindaysa dokunma
        if (phone.StartsWith("+") && phone.Contains('-'))
            return phone;

        // Rakamlari cikar
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // +905351234567 veya 905351234567 (12 hane, 90 ile basliyor)
        if (digits.Length == 12 && digits.StartsWith("90"))
            return $"+90-{digits[2..]}";

        // 05351234567 (11 hane, 0 ile basliyor)
        if (digits.Length == 11 && digits.StartsWith("0"))
            return $"+90-{digits[1..]}";

        // 5351234567 (10 hane � TR varsay)
        if (digits.Length == 10)
            return $"+90-{digits}";

        // Diger durumlarda oldu�u gibi d�n
        return phone.StartsWith("+") ? phone : $"+{phone}";
    }
}