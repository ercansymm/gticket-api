using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text; 
using System.Xml.Linq;


namespace GBILET.Infrastructure.Services;

public class BiletBankFlightService : IFlightService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BiletBankFlightService> _logger;
    private readonly string _clientName;
    private readonly string _password;
    private readonly string _username;
    private readonly string _proxyUrl;

    public BiletBankFlightService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BiletBankFlightService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

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
        return response;
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
        return new FlightSegment
        {
            SegmentId = seg.GetValue("SegmentId"),
            SequenceNo = seg.GetIntValue("SequenceNo"),
            OriginCode = seg.GetValue("OriginCode"),
            DestinationCode = seg.GetValue("DestinationCode"),
            OD_OriginCode = seg.GetValue("OD_OriginCode"),
            OD_DestinationCode = seg.GetValue("OD_DestinationCode"),
            DepartureDay = seg.GetValue("DepartureDay"),
            DepartureTime = seg.GetValue("DepartureTime"),
            ArrivalDay = seg.GetValue("ArrivalDay"),
            ArrivalTime = seg.GetValue("ArrivalTime"),
            MarketingAirline = seg.GetValue("MarketingAirline"),
            OperatingAirline = seg.GetValue("OperatingAirline"),
            FlightNumber = seg.GetValue("FlightNumber"),
            BookingClass = seg.GetValue("BookingClass"),
            FareBasis = seg.GetValue("FareBasis"),
            FareType = seg.GetValue("FareType"),
            Equipment = seg.GetValue("Equipment"),
            Duration = seg.GetIntValue("Duration"),
            SelectedBrandedFareItemId = seg.GetValue("SelectedBrandedFareItemId")
        };
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
            HasError = false,
            SessionId = doc.GetValue("SessionId"),
            SessionToken = doc.GetValue("SessionToken")
        };

        // Debug: root element isimlerini topla
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

            // AirBookings - T_AirBooking ust seviye, icinde T_AirBookingItem ve T_Segment var
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
                    Currency = priceSummary.GetValue("Currency")
                };

                // GrandTotal dogrudan PriceSummary icinde degilse T_PriceItem icinden topla
                if (response.PriceSummary.GrandTotal == 0)
                {
                    var priceItems = priceSummary.GetDescendants("T_PriceItem");
                    decimal grandTotal = 0;
                    foreach (var pi in priceItems)
                    {
                        // T_PriceItem icinde Total veya TotalFare olabilir
                        var itemTotal = pi.GetDecimalValue("Total");
                        if (itemTotal == 0)
                            itemTotal = pi.GetDecimalValue("TotalFare");
                        grandTotal += itemTotal;
                    }
                    if (grandTotal > 0)
                        response.PriceSummary.GrandTotal = grandTotal;
                }
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
        // T_AirBooking seviyesinde: ProductId, BookingCode, BookingProvider, Status
        // T_AirBookingItem seviyesinde: Currency, BaseFare, Taxes, TotalFare, ServiceFee
        var bookingItem = ab.GetDescendants("T_AirBookingItem").FirstOrDefault();

        var booking = new AllocateAirBooking
        {
            ProductId = ab.GetValue("ProductId"),
            PNR = ab.GetValue("BookingCode"),
            BookingProvider = ab.GetValue("BookingProvider"),
            Status = ab.GetValue("Status") ?? ab.GetValue("SelectedAllocated"),
            Currency = bookingItem?.GetValue("Currency") ?? ab.GetValue("Currency"),
            TotalFare = bookingItem?.GetDecimalValue("TotalFare") ?? ab.GetDecimalValue("TotalFare"),
            BaseFare = bookingItem?.GetDecimalValue("BaseFare") ?? ab.GetDecimalValue("BaseFare"),
            Taxes = bookingItem?.GetDecimalValue("Taxes") ?? ab.GetDecimalValue("Taxes"),
            ServiceFee = bookingItem?.GetDecimalValue("ServiceFee") ?? ab.GetDecimalValue("ServiceFee")
        };

        // Status alani XML'de "SelectedAllocated" gibi bir deger olarak gelebilir
        // Eger Status hala null ise Descendants icinde ara
        if (string.IsNullOrEmpty(booking.Status))
        {
            booking.Status = ab.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "Status")?.Value;
        }

        // T_Segment'ler T_AirBooking veya T_AirBookingItem icinde olabilir
        foreach (var seg in ab.GetDescendants("T_Segment"))
        {
            booking.Segments.Add(new AllocateSegment
            {
                OriginCode = seg.GetValue("OriginCode"),
                DestinationCode = seg.GetValue("DestinationCode"),
                DepartureDay = seg.GetValue("DepartureDay"),
                DepartureTime = seg.GetValue("DepartureTime"),
                ArrivalDay = seg.GetValue("ArrivalDay"),
                ArrivalTime = seg.GetValue("ArrivalTime"),
                MarketingAirline = seg.GetValue("MarketingAirline"),
                OperatingAirline = seg.GetValue("OperatingAirline"),
                FlightNumber = seg.GetValue("FlightNumber"),
                BookingClass = seg.GetValue("BookingClass")
            });
        }

        return booking;
    }

    #endregion
}