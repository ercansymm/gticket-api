using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text; 
using System.Xml.Linq;


namespace GBILET.Infrastructure.Services;

public class BiletBankFlightService : IFlightService
{
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

        var response = await AirSearchAsync(loginResult.SessionId!, loginResult.SessionToken!, request);
        response.SessionId = loginResult.SessionId;
        response.SessionToken = loginResult.SessionToken;
        return response;
    }

    public async Task<FlightSearchResponseDto> SearchFlightDtoAsync(SearchRequest request)
    {
        var rawResponse = await SearchFlightAsync(request);
        var dto = FlightSearchMapper.MapToDto(rawResponse, _logger);

        // Session bilgilerini cache'le (sonraki adımlarda allocate/booking için)
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

        var inner = await UpdatePassengersInternalAsync(request.SessionId, request.SessionToken, request);
        return inner;
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

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/I_Authentication/Login");

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

            var doc = XDocument.Parse(responseText);

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
                loginResponse.ErrorMessage ??= "Login yanıtında SessionId bulunamadı.";
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

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/I_Shopping/AirSearch");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[AirSearch] SOAP Response:\n{SoapResponse}", responseText);

            var doc = XDocument.Parse(responseText);

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                return new AirSearchResponse
                {
                    HasError = true,
                    ErrorMessage = doc.GetValue("Message") ?? doc.GetValue("ServiceError")
                };
            }

            return ParseAirSearchResponse(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AirSearch] Exception");
            return new AirSearchResponse
            {
                HasError = true,
                ErrorMessage = $"AirSearch hatası: {ex.Message}"
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
        segments.Append($@"
                <trev2:T_AirSearch_SegmentItem>
                   <trev2:DepartureDay>{request.DepartureDate:yyyy-MM-dd}T00:00:00.000+00:00</trev2:DepartureDay>
                   <trev2:Destination>
                      <trev2:Code>{request.Destination}</trev2:Code>
                      <trev2:CountryCode>{request.DestinationCountryCode}</trev2:CountryCode>
                      <trev2:IsCity>{request.DestinationIsCity.ToString().ToLower()}</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Destination>
                   <trev2:Origin>
                      <trev2:Code>{request.Origin}</trev2:Code>
                      <trev2:CountryCode>{request.OriginCountryCode}</trev2:CountryCode>
                      <trev2:IsCity>{request.OriginIsCity.ToString().ToLower()}</trev2:IsCity>
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
                      <trev2:IsCity>{request.OriginIsCity.ToString().ToLower()}</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Destination>
                   <trev2:Origin>
                      <trev2:Code>{request.Destination}</trev2:Code>
                      <trev2:CountryCode>{request.DestinationCountryCode}</trev2:CountryCode>
                      <trev2:IsCity>{request.DestinationIsCity.ToString().ToLower()}</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Origin>
                   <trev2:SequenceNo>2</trev2:SequenceNo>
                </trev2:T_AirSearch_SegmentItem>");
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
                <trev2:IfDirectFlightsOnly>{request.DirectFlightsOnly.ToString().ToLower()}</trev2:IfDirectFlightsOnly>
               <trev2:IfRefundablesOnly>{request.RefundablesOnly.ToString().ToLower()}</trev2:IfRefundablesOnly>
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

        var flightOptions = doc.GetDescendants("T_FlightOption");
        foreach (var fo in flightOptions)
        {
            response.FlightOptions.Add(ParseFlightOption(fo));
        }

        var recommendationBoxes = doc.GetDescendants("T_RecommendationBox");
        foreach (var rb in recommendationBoxes)
        {
            response.RecommendationBoxes.Add(ParseRecommendationBox(rb));
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

        var brandedFaresElement = fo.GetElement("BrandedFares");
        if (brandedFaresElement != null)
        {
            foreach (var bfi in brandedFaresElement.GetElements("BrandedFareItem"))
            {
                option.BrandedFareItems.Add(ParseBrandedFareItem(bfi));
            }

            foreach (var bi in brandedFaresElement.GetElements("BrandedItem"))
            {
                var existingItem = option.BrandedFareItems.FirstOrDefault();
                existingItem?.BrandedItems.Add(ParseBrandedItem(bi));
            }
        }

        var baggageElement = fo.GetElement("FreeBaggageAllowance");
        if (baggageElement != null)
        {
            foreach (var pba in baggageElement.GetDescendants("PassengerBaggageAllowance"))
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
        var rawDepartureTime = seg.GetValue("DepartureTime");
        var rawArrivalTime = seg.GetValue("ArrivalTime");
        var rawDuration = seg.GetValue("Duration");

        return new FlightSegment
        {
            SegmentId = seg.GetValue("SegmentId"),
            SequenceNo = seg.GetIntValue("SequenceNo"),
            OriginCode = seg.GetValue("OriginCode"),
            DestinationCode = seg.GetValue("DestinationCode"),
            OD_OriginCode = seg.GetValue("OD_OriginCode"),
            OD_DestinationCode = seg.GetValue("OD_DestinationCode"),
            DepartureDay = FormatDay(seg.GetValue("DepartureDay")),
            DepartureTime = FormatIso8601DurationAsTime(rawDepartureTime),
            ArrivalDay = FormatDay(seg.GetValue("ArrivalDay")),
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

        foreach (var of in rb.GetDescendants("OutboundFlight"))
        {
            box.OutboundFlights.Add(ParseRecommendationFlight(of));
        }

        foreach (var inf in rb.GetDescendants("InboundFlight"))
        {
            box.InboundFlights.Add(ParseRecommendationFlight(inf));
        }

        var brandedFaresElement = rb.GetElement("BrandedFares");
        if (brandedFaresElement != null)
        {
            foreach (var bfi in brandedFaresElement.GetElements("BrandedFareItem"))
            {
                box.BrandedFareItems.Add(ParseBrandedFareItem(bfi));
            }
        }

        return box;
    }

    private static RecommendationFlight ParseRecommendationFlight(XElement flight)
    {
        var rf = new RecommendationFlight
        {
            FlightId = flight.GetValue("FlightId"),
            Duration = flight.GetIntValue("Duration")
        };

        foreach (var seg in flight.GetDescendants("T_Segment"))
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

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/I_Shopping/Allocate");

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

            var doc = XDocument.Parse(responseText);

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
               <trev1:IO_AllocationItem>
                  <trev1:ProductId>{request.ProductId}</trev1:ProductId>
                  <trev1:SelectedServiceFee>
                     <trev1:Amount>{request.SelectedServiceFee.ToString(System.Globalization.CultureInfo.InvariantCulture)}</trev1:Amount>
                  </trev1:SelectedServiceFee>
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

        // Passengers (T_Passenger) — TempTag degerlerini parse et
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
                // TempTag olarak PaxReferenceId kullanilmali — yoksa BiletBank API eslestirme yapamiyor
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

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/I_Shopping/UpdatePassengers");

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

            var doc = XDocument.Parse(responseText);
            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                var errorMsg = doc.GetValue("ErrorMessage")
                    ?? doc.GetValue("DebugMessage")
                    ?? doc.GetValue("Message")
                    ?? doc.GetValue("ServiceError");
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

            // WSDL alphabetical order: BirthDate, CitizenNo, DestinationAddress, Email, FirstName,
            // FrequentFlayerNo, Gender, HesCode, Id, IfContact, LastName, Nationality, PassportCountry,
            // PassportNo, PassportValidDate, PaxReferences, Phone, SecondaryPhoneNumber, SequenceNo,
            // TempTag, Type, WheelChairServiceType
            passengersXml.Append($@"
            <trev2:T_Passenger>
              <trev2:BirthDate>{birthDate}</trev2:BirthDate>
              <trev2:CitizenNo>{pax.CitizenNo ?? "00000000000"}</trev2:CitizenNo>
              <trev2:DestinationAddress i:nil=""true""/>
              <trev2:Email>{(isContact ? request.Contact.Email : "")}</trev2:Email>
              <trev2:FirstName>{pax.FirstName}</trev2:FirstName>
              <trev2:FrequentFlayerNo i:nil=""true""/>
              <trev2:Gender>{pax.Gender}</trev2:Gender>
              <trev2:HesCode i:nil=""true""/>
              <trev2:Id>{paxId}</trev2:Id>
              <trev2:IfContact>{isContact.ToString().ToLower()}</trev2:IfContact>
              <trev2:LastName>{pax.LastName}</trev2:LastName>
              <trev2:Nationality>{pax.Nationality}</trev2:Nationality>
              <trev2:PassportCountry>{pax.PassportCountry ?? pax.Nationality}</trev2:PassportCountry>
              {(string.IsNullOrEmpty(pax.PassportNo) ? "<trev2:PassportNo i:nil=\"true\"/>" : $"<trev2:PassportNo>{pax.PassportNo}</trev2:PassportNo>")}
              <trev2:PassportValidDate i:nil=""true""/>
              <trev2:PaxReferences>
                <trev:T_ForwardPaxReference>
                  <trev:PaxReferenceId>{pax.PaxReferenceId}</trev:PaxReferenceId>
                  <trev:ProductId>{request.ProductId}</trev:ProductId>
                  <trev:ProductItemId>{request.ProductItemId}</trev:ProductItemId>
                  <trev:SequenceNo>{i + 1}</trev:SequenceNo>
                </trev:T_ForwardPaxReference>
              </trev2:PaxReferences>
              <trev2:Phone>{(isContact ? request.Contact.Phone : "")}</trev2:Phone>
              <trev2:SecondaryPhoneNumber i:nil=""true""/>
              <trev2:SequenceNo>{i + 1}</trev2:SequenceNo>
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
        var soapRequest = BuildMakePreBookingSoapRequest(
            request.SessionId,
            request.SessionToken,
            request.ProductId,
            request.BrandedFareItemId,
            request.ShoppingFileId);

        _logger.LogInformation("[MakePreBooking] SOAP Request:\n{SoapRequest}", soapRequest);

        var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", "http://tempuri.org/I_Shopping/MakePrebooking");

        try
        {
            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

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

            var doc = XDocument.Parse(responseText);

            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                return new MakePreBookingResponse
                {
                    HasError = true,
                    ErrorMessage = doc.GetValue("ErrorMessage")
                        ?? doc.GetValue("DebugMessage")
                        ?? doc.GetValue("Message")
                        ?? doc.GetValue("ServiceError")
                };
            }

            return ParseMakePreBookingResponse(doc);
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
         <trev1:Form>
            <trev1:Branded>
               <trev1:IO_Air_Branded_Form>
                  <trev1:BrandedFareItemId>{brandedFareItemId}</trev1:BrandedFareItemId>
                  <trev1:ProductId>{productId}</trev1:ProductId>
               </trev1:IO_Air_Branded_Form>
            </trev1:Branded>
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

        // AirBooking bilgileri
        var airBooking = doc.GetDescendants("T_AirBooking").FirstOrDefault();
        if (airBooking != null)
        {
            response.BookingCode = airBooking.GetValue("BookingCode");
            response.ProductId = airBooking.GetValue("ProductId");
            response.Status = airBooking.GetValue("Status");
            response.BaseFare = airBooking.GetDecimalValue("BaseFare");
            response.Taxes = airBooking.GetDecimalValue("Taxes");
            response.ServiceFee = airBooking.GetDecimalValue("ServiceFee");
            response.TotalFare = airBooking.GetDecimalValue("TotalFare");

            var ruleAttr = airBooking.GetDescendants("FlightRuleAttribute").FirstOrDefault();
            if (ruleAttr != null)
                response.CanBeReserved = ruleAttr.GetBoolValue("IsReservable");
        }

        // TimeTable — on rezervasyon ve rezervasyon gecerlilik sureleri
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

        // Segmentler
        foreach (var seg in doc.GetDescendants("T_Segment"))
        {
            response.Segments.Add(new PreBookingSegment
            {
                SegmentId = seg.GetValue("Id"),
                OriginCode = seg.GetValue("OriginCode"),
                DestinationCode = seg.GetValue("DestinationCode"),
                DepartureDay = seg.GetValue("DepartureDay"),
                DepartureTime = seg.GetValue("DepartureTime"),
                ArrivalDay = seg.GetValue("ArrivalDay"),
                ArrivalTime = seg.GetValue("ArrivalTime"),
                FlightNumber = seg.GetValue("FlightNumber"),
                MarketingAirline = seg.GetValue("MarketingAirline"),
                BookingClass = seg.GetValue("BookingClass")
            });
        }

        return response;
    }

    #endregion

    #region RemoveProduct

    public async Task<RemoveProductResponse> RemoveProductAsync(RemoveProductRequest request)
    {
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
         <trev1:Form>
            <trev1:ProductIds>
               <arr:guid>{request.ProductId}</arr:guid>
            </trev1:ProductIds>
         </trev1:Form>
      </tem:request>
   </tem:RemoveProduct>
</soap:Body>
</soap:Envelope>";

        try
        {
            _logger.LogInformation("[RemoveProduct] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/I_Shopping/RemoveProduct");

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
            var amount = request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var currency = request.Currency ?? "TRY";
            var sessionId = request.SessionId ?? "";
            var sessionToken = request.SessionToken ?? "";
            var shoppingFileId = request.ShoppingFileId ?? "";

            var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
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
         <trev1:DeductLastSellerCommission>false</trev1:DeductLastSellerCommission>
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

            _logger.LogInformation("[MakePayment] SOAP Request (card masked)");

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/I_Shopping/MakePayment_FromRunningAccount");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[MakePayment] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[MakePayment] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new MakePaymentResponse
                {
                    HasError = true,
                    ErrorMessage = $"MakePayment HTTP {(int)response.StatusCode}: {responseText}"
                };
            }

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return new MakePaymentResponse
                {
                    HasError = true,
                    ErrorMessage = "MakePayment: Bos response alindi."
                };
            }

            var doc = XDocument.Parse(responseText);

            var hasErrorVal = doc.GetValue("HasError");
            if (hasErrorVal == "true")
            {
                return new MakePaymentResponse
                {
                    HasError = true,
                    ErrorMessage = doc.GetValue("ErrorMessage") ?? doc.GetValue("Message") ?? doc.GetValue("ServiceError")
                };
            }

            var shoppingFileEl = doc.GetDescendants("ShoppingFile").FirstOrDefault();
            var paymentId = doc.GetValue("PaymentId");

            return new MakePaymentResponse
            {
                HasError = false,
                IsPaymentSuccessful = true,
                Status = shoppingFileEl?.GetValue("Status") ?? "Paid",
                ShoppingFileId = shoppingFileEl?.GetValue("Id"),
                RemainingSum = shoppingFileEl != null ? shoppingFileEl.GetDecimalValue("RemainingSum") : 0,
                Currency = shoppingFileEl?.GetValue("Currency") ?? currency,
                PaymentReferenceId = paymentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MakePayment] Exception");
            return new MakePaymentResponse
            {
                HasError = true,
                ErrorMessage = $"MakePayment hatasi: {ex.Message}"
            };
        }
    }

    #endregion

    #region FinalizeShopping

    public async Task<FinalizeShoppingResponse> FinalizeShoppingAsync(FinalizeShoppingRequest request)
    {
        var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping""
xmlns:arr=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"">
<soap:Body>
   <tem:FinalizeShopping>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{request.SessionId}</trev:SessionId>
            <trev:SessionToken>{request.SessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev:ExtraParamList>
            <trev:ExtendedData>
               <trev:Name>IntendedShoppingFileId</trev:Name>
               <trev:Value>{request.ShoppingFileId}</trev:Value>
            </trev:ExtendedData>
         </trev:ExtraParamList>
         <trev1:Form>
            <trev1:ProductIds>
               <arr:guid>{request.ProductId}</arr:guid>
            </trev1:ProductIds>
         </trev1:Form>
      </tem:request>
   </tem:FinalizeShopping>
</soap:Body>
</soap:Envelope>";

        try
        {
            _logger.LogInformation("[FinalizeShopping] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/I_Shopping/FinalizeShopping");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[FinalizeShopping] HTTP Status: {StatusCode}", (int)response.StatusCode);
            _logger.LogInformation("[FinalizeShopping] SOAP Response:\n{SoapResponse}", responseText);

            if (!response.IsSuccessStatusCode)
            {
                return new FinalizeShoppingResponse
                {
                    HasError = true,
                    ErrorMessage = $"FinalizeShopping HTTP {(int)response.StatusCode}: {responseText}"
                };
            }

            var doc = XDocument.Parse(responseText);
            var hasError = doc.GetValue("HasError");
            if (hasError == "true")
            {
                return new FinalizeShoppingResponse
                {
                    HasError = true,
                    ErrorMessage = doc.GetValue("ErrorMessage") ?? doc.GetValue("Message") ?? doc.GetValue("ServiceError")
                };
            }

            return ParseFinalizeShoppingResponse(doc);
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

        var airBooking = doc.GetDescendants("T_AirBooking").FirstOrDefault();
        if (airBooking != null)
        {
            result.BookingCode = airBooking.GetValue("BookingCode");
            result.Status = airBooking.GetValue("Status");
            result.TotalFare = airBooking.GetDecimalValue("TotalFare");
        }

        // E-bilet numaralarini topla
        int seqNo = 0;
        foreach (var pax in doc.GetDescendants("T_Passenger"))
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

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/I_Shopping/PokeShoppingFile");

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

            var doc = XDocument.Parse(responseText);
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

            return new PokeShoppingFileResponse
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
                GrandTotal = priceSummary?.GetDecimalValue("GrandTotal") ?? 0
            };
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
        var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:tem=""http://tempuri.org/""
xmlns:trev=""http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base""
xmlns:trev1=""http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping"">
<soap:Body>
   <tem:ReadShoppingFile>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>{request.SessionId}</trev:SessionId>
            <trev:SessionToken>{request.SessionToken}</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev1:Form>
            <trev1:ShoppingFileId>{request.ShoppingFileId}</trev1:ShoppingFileId>
         </trev1:Form>
      </tem:request>
   </tem:ReadShoppingFile>
</soap:Body>
</soap:Envelope>";

        try
        {
            _logger.LogInformation("[ReadShoppingFile] SOAP Request:\n{SoapRequest}", soapRequest);

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/I_Shopping/ReadShoppingFile");

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

            var doc = XDocument.Parse(responseText);
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
                result.GrandTotal = priceSummary.GetDecimalValue("GrandTotal");
        }

        var airBooking = doc.GetDescendants("T_AirBooking").FirstOrDefault();
        if (airBooking != null)
        {
            result.BookingCode = airBooking.GetValue("BookingCode");
            result.Status = airBooking.GetValue("Status");
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

        // Segmentler
        foreach (var seg in doc.GetDescendants("T_Segment"))
        {
            result.Segments.Add(new PreBookingSegment
            {
                SegmentId = seg.GetValue("Id"),
                OriginCode = seg.GetValue("OriginCode"),
                DestinationCode = seg.GetValue("DestinationCode"),
                DepartureDay = seg.GetValue("DepartureDay"),
                DepartureTime = seg.GetValue("DepartureTime"),
                ArrivalDay = seg.GetValue("ArrivalDay"),
                ArrivalTime = seg.GetValue("ArrivalTime"),
                FlightNumber = seg.GetValue("FlightNumber"),
                MarketingAirline = seg.GetValue("MarketingAirline"),
                BookingClass = seg.GetValue("BookingClass")
            });
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

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/I_Authentication/Logout");

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

            var doc = XDocument.Parse(responseText);
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
}