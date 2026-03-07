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
        _httpClient.DefaultRequestHeaders.Host = "apitest.biletbank.com";

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
        return response;
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

            var doc = XDocument.Parse(responseText);

            var hasError = doc.GetValue("HasError");

            var loginResponse = new LoginResponse
            {
                SessionId = doc.GetValue("SessionId"),
                SessionToken = doc.GetValue("SessionToken"),
                HasError = hasError == "true",
                ErrorMessage = hasError == "true" ? doc.GetValue("Message") : null,
                ServiceError = doc.GetValue("ServiceError")
            };

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
            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://tempuri.org/ITrevooWS/AirSearch");

            var response = await _httpClient.PostAsync(_proxyUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

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
                  <trev2:DepartureDay>{request.DepartureDate:yyyy-MM-dd}T00:00:00</trev2:DepartureDay>
                  <trev2:Origin>
                     <trev2:Code>{request.Origin}</trev2:Code>
                     <trev2:CountryCode>{request.OriginCountryCode}</trev2:CountryCode>
                     <trev2:IsCity>{request.OriginIsCity.ToString().ToLower()}</trev2:IsCity>
                     <trev2:Name/>
                  </trev2:Origin>
                  <trev2:Destination>
                     <trev2:Code>{request.Destination}</trev2:Code>
                     <trev2:CountryCode>{request.DestinationCountryCode}</trev2:CountryCode>
                     <trev2:IsCity>{request.DestinationIsCity.ToString().ToLower()}</trev2:IsCity>
                     <trev2:Name/>
                  </trev2:Destination>
                  <trev2:SequenceNo>1</trev2:SequenceNo>
               </trev2:T_AirSearch_SegmentItem>");

        if (request.FlightType == "RT" && request.ReturnDate.HasValue)
        {
            segments.Append($@"
               <trev2:T_AirSearch_SegmentItem>
                  <trev2:DepartureDay>{request.ReturnDate.Value:yyyy-MM-dd}T00:00:00</trev2:DepartureDay>
                  <trev2:Origin>
                     <trev2:Code>{request.Destination}</trev2:Code>
                     <trev2:CountryCode>{request.DestinationCountryCode}</trev2:CountryCode>
                     <trev2:IsCity>{request.DestinationIsCity.ToString().ToLower()}</trev2:IsCity>
                     <trev2:Name/>
                  </trev2:Origin>
                  <trev2:Destination>
                     <trev2:Code>{request.Origin}</trev2:Code>
                     <trev2:CountryCode>{request.OriginCountryCode}</trev2:CountryCode>
                     <trev2:IsCity>{request.OriginIsCity.ToString().ToLower()}</trev2:IsCity>
                     <trev2:Name/>
                  </trev2:Destination>
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
               <trev2:FlightWithBaggage>{request.FlightWithBaggage.ToString().ToLower()}</trev2:FlightWithBaggage>
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

        var flightOptions = doc.GetDescendants("FlightOption");
        foreach (var fo in flightOptions)
        {
            response.FlightOptions.Add(ParseFlightOption(fo));
        }

        var recommendationBoxes = doc.GetDescendants("RecommendationBox");
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

        foreach (var seg in fo.GetDescendants("Segment"))
        {
            option.Segments.Add(ParseSegment(seg));
        }

        foreach (var sa in fo.GetDescendants("SegmentAvailability"))
        {
            option.SegmentAvailabilities.Add(new SegmentAvailability
            {
                SegmentSequenceNo = sa.GetIntValue("SegmentSequenceNo"),
                BookingClassCode = sa.GetValue("BookingClassCode"),
                AvailableSeats = sa.GetIntValue("AvailableSeats"),
                IsPromo = sa.GetBoolValue("IsPromo")
            });
        }

        foreach (var pfi in fo.GetDescendants("PassengerFareItem"))
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

        foreach (var seg in flight.GetDescendants("Segment"))
        {
            rf.Segments.Add(ParseSegment(seg));
        }

        return rf;
    }
}