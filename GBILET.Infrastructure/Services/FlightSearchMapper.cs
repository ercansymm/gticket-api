using GBILET.Core.Helpers;
using GBILET.Core.Models.Flight;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace GBILET.Infrastructure.Services;

public static class FlightSearchMapper
{
    public static FlightSearchResponseDto MapToDto(
        AirSearchResponse response,
        SearchRequest request,
        ILogger? logger = null,
        string? requestedFlightClass = null)
    {
        var dto = new FlightSearchResponseDto
        {
            HasError = response.HasError,
            ErrorMessage = response.ErrorMessage,
            SearchId = response.SearchId,
            SessionId = response.SessionId,
            SessionToken = response.SessionToken
        };

        if (response.HasError)
            return dto;

        // RecommendationBox'taki BrandedFareItems'ı ProductId bazlı index'le
        // BrandedFareVersion=v2 kullanıldığında paket bilgileri T_FlightOption'da değil
        // T_RecommendationBox altında döner
        var rbBrandedFaresByProductId = new Dictionary<string, List<BrandedFareItem>>();
        foreach (var rb in response.RecommendationBoxes)
        {
            if (!string.IsNullOrEmpty(rb.ProductId) && rb.BrandedFareItems.Count > 0)
            {
                rbBrandedFaresByProductId[rb.ProductId] = rb.BrandedFareItems;
            }
        }

        foreach (var option in response.FlightOptions)
        {
            // FlightOption'da BrandedFareItems boşsa RecommendationBox'tan al
            if (option.BrandedFareItems.Count == 0
                && !string.IsNullOrEmpty(option.ProductId)
                && rbBrandedFaresByProductId.TryGetValue(option.ProductId, out var rbBrandedFares))
            {
                option.BrandedFareItems = rbBrandedFares;
            }

            var flight = MapFlightOption(option, request, logger);
            dto.Flights.Add(flight);
        }

        // FlightOption yoksa (RT aramalarda BiletBank yalnızca RecommendationBox dönebilir):
        // Her RecommendationBox'ı gidiş + dönüş olarak iki ayrı FlightResultDto'ya dönüştür.
        if (response.FlightOptions.Count == 0 && response.RecommendationBoxes.Count > 0)
        {
            foreach (var rb in response.RecommendationBoxes)
            {
                var rbFlights = MapRecommendationBox(rb, request, logger);
                dto.Flights.AddRange(rbFlights);
            }
        }

        // BiletBank may return flights from all cabin classes even when a specific
        // FlightClass was requested. Filter to only the requested class.
        if (!string.IsNullOrEmpty(requestedFlightClass))
        {
            var before = dto.Flights.Count;
            dto.Flights = dto.Flights
                .Where(f => string.Equals(f.CabinClass, requestedFlightClass, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (dto.Flights.Count < before)
            {
                logger?.LogInformation(
                    "[MapToDto] Filtered flights by requested cabin class '{RequestedClass}': {Before} -> {After}",
                    requestedFlightClass, before, dto.Flights.Count);
            }
        }

        dto.FilterOptions = BuildFilterOptions(dto.Flights);

        // DEBUG: Mapping bittikten sonra tüm sayıları topla — flights boş geliyorsa tanı için
        var brandElements = response.DebugElementNames?
            .Where(e => e.Contains("Brand", StringComparison.OrdinalIgnoreCase)
                     || e.Contains("Baggage", StringComparison.OrdinalIgnoreCase)
                     || e.Contains("FreeBag", StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];

        var firstRb = response.RecommendationBoxes.FirstOrDefault();

        dto._debug = new
        {
            totalElementNames = response.DebugElementNames?.Count ?? 0,
            brandRelatedElements = brandElements,
            allElementNames = response.DebugElementNames,
            flightOptionCount = response.FlightOptions.Count,
            recommendationBoxCount = response.RecommendationBoxes.Count,
            mappedFlightCount = dto.Flights.Count,
            firstFlightOptionBrandedFareCount = response.FlightOptions.FirstOrDefault()?.BrandedFareItems.Count ?? -1,
            firstRecommendationBoxBrandedFareCount = response.RecommendationBoxes.FirstOrDefault()?.BrandedFareItems.Count ?? -1,
            // RecommendationBox segment tanısı — flights boş geliyorsa buraya bak
            firstRbOutboundFlightCount = firstRb?.OutboundFlights.Count ?? -1,
            firstRbInboundFlightCount = firstRb?.InboundFlights.Count ?? -1,
            firstRbOtherFlightCount = firstRb?.OtherFlights.Count ?? -1,
            firstRbFirstOutboundSegmentCount = firstRb?.OutboundFlights.FirstOrDefault()?.Segments.Count ?? -1,
            firstRbFirstInboundSegmentCount = firstRb?.InboundFlights.FirstOrDefault()?.Segments.Count ?? -1,
            firstFlightOptionXml = response.DebugFirstFlightOptionXml,
            firstRecommendationBoxXml = response.DebugFirstRecommendationBoxXml,
            subSearchErrors = response.SubSearchErrors,
        };

        return dto;
    }

    /// <summary>
    /// Tüm yolcuların TOPLAM fiyatını hesaplar: Σ (paxItem.TotalFare × yolcu sayısı).
    /// ÖNEMLİ: BiletBank acente komisyonunu (CustomerCommission.Value) ServiceFee'ye ZATEN dahil ediyor
    /// (ServiceFee = SystemServiceFee + CustomerCommission.Value, TotalFare = BaseFare + Taxes + ServiceFee).
    /// Bu yüzden komisyon TEKRAR EKLENMEZ — paxItem.TotalFare olduğu gibi kullanılır.
    /// Pax fare öğeleri yoksa kişi başı fiyat × toplam yolcu sayısı fallback'i kullanılır.
    /// </summary>
    private static decimal CalcGrandTotal(
        decimal perPaxTotal, IEnumerable<PassengerFareItem> paxItems, SearchRequest req)
    {
        decimal total = 0;
        bool any = false;
        foreach (var pfi in paxItems)
        {
            var count = BiletBankFlightService.PaxCountFor(pfi.PaxCode, req);
            if (count <= 0) continue;
            total += pfi.TotalFare * count;
            any = true;
        }
        if (any) return total;

        var totalPax = req.AdultCount + req.ChildCount + req.InfantCount;
        return perPaxTotal * Math.Max(totalPax, 1);
    }

    /// <summary>
    /// Bir RecommendationBox'ı FlightResultDto listesine dönüştürür:
    ///   1. Gidiş bacağı (OutboundFlights) — IsRoundTripBundle=true, IsReturnLeg=false
    ///   2. Dönüş bacağı (InboundFlights)  — IsRoundTripBundle=true, IsReturnLeg=true
    ///   3+ Diğer bacaklar (OtherFlights) — MP aramalarda 3. ve sonraki bacaklar
    /// Her DTO aynı ProductId'yi paylaşır (bundle tek bir allocate ile rezerve edilir).
    /// </summary>
    private static List<FlightResultDto> MapRecommendationBox(RecommendationBox rb, SearchRequest request, ILogger? logger)
    {
        var result = new List<FlightResultDto>();

        logger?.LogInformation(
            "[MapRecommendationBox] ProductId={ProductId}, BrandedFareItems={BrandedFareItemCount}, OutboundFlights={OutboundCount}, InboundFlights={InboundCount}, OtherFlights={OtherCount}",
            rb.ProductId, rb.BrandedFareItems.Count, rb.OutboundFlights.Count, rb.InboundFlights.Count, rb.OtherFlights.Count);

        // Gidiş bacakları
        foreach (var outbound in rb.OutboundFlights)
        {
            var dto = MapRecommendationFlight(outbound, rb, isReturnLeg: false, request, logger);
            if (dto != null) result.Add(dto);
        }

        // Dönüş bacakları — segment SequenceNo'ları 2'ye zorla (frontend split için)
        foreach (var inbound in rb.InboundFlights)
        {
            // Inbound segmentlerin SequenceNo'sunu 2 yap
            foreach (var seg in inbound.Segments)
                seg.SequenceNo = 2;

            var dto = MapRecommendationFlight(inbound, rb, isReturnLeg: true, request, logger);
            if (dto != null) result.Add(dto);
        }

        // OtherFlights: MP (Multi-city) 3. ve sonraki bacaklar — SequenceNo=3+ olarak zorla
        for (int i = 0; i < rb.OtherFlights.Count; i++)
        {
            var other = rb.OtherFlights[i];
            var legNo = 3 + i;
            // 3. bacaktan itibaren SequenceNo ata (3, 4, 5...)
            foreach (var seg in other.Segments)
                seg.SequenceNo = legNo;

            var dto = MapRecommendationFlight(other, rb, isReturnLeg: false, request, logger);
            if (dto != null)
            {
                // Unique ProductId — outbound ile çakışmasın (React key + frontend tanımlama)
                var otherOrigin = other.Segments.FirstOrDefault()?.OriginCode ?? "";
                dto.ProductId = $"{rb.ProductId}_leg{legNo}_{other.FlightId ?? otherOrigin}";
                result.Add(dto);
            }
        }

        return result;
    }

    private static FlightResultDto? MapRecommendationFlight(
        RecommendationFlight flight,
        RecommendationBox rb,
        bool isReturnLeg,
        SearchRequest request,
        ILogger? logger)
    {
        if (flight.Segments.Count == 0) return null;

        var firstSeg = flight.Segments.First();
        var lastSeg = flight.Segments.Last();

        var segmentDtos = new List<FlightSegmentDto>();
        for (int i = 0; i < flight.Segments.Count; i++)
        {
            var seg = flight.Segments[i];
            var segDto = MapSegment(seg);

            if (i > 0)
            {
                var prevSeg = flight.Segments[i - 1];
                var layover = CalculateLayoverMinutes(prevSeg, seg);
                if (layover.HasValue)
                {
                    segDto.LayoverMinutes = layover.Value;
                    segDto.LayoverFormatted = FormatDuration(layover.Value);
                }
            }

            segmentDtos.Add(segDto);
        }

        var (totalHours, totalMinutes, totalDurationMinutes) = CalculateTotalDuration(firstSeg, lastSeg);
        int stopCount = flight.Segments.Count - 1;
        bool isDirect = stopCount == 0;
        string stopText = BuildStopText(flight.Segments, stopCount, isDirect);

        string airlineCode = firstSeg.MarketingAirline ?? "";
        string airlineName = FlightMappings.GetAirlineName(airlineCode);
        string originCode = firstSeg.OriginCode ?? "";
        string destinationCode = lastSeg.DestinationCode ?? "";
        var cabinClass = DetermineCabinClass(firstSeg.BookingClass, firstSeg.FareType);
        var cabinClassName = FlightMappings.GetFareTypeName(cabinClass);

        // ProductId: gidiş ve dönüş bacağı aynı box.ProductId'yi paylaşır.
        // Dönüş bacağına "_ret" suffix ekleriz ki frontend duplikat olarak görmesin.
        // Allocate'te BundleProductId (asıl) kullanılır.
        var productId = isReturnLeg
            ? $"{rb.ProductId}_ret_{flight.FlightId ?? originCode}"
            : rb.ProductId;

        // Fiyat: BiletBank komisyonu ServiceFee/TotalFare'a zaten dahil. GrandTotal = tüm yolcuların toplamı.
        var rbCurrency = rb.Currency ?? "TRY";
        var rbGrandTotal = CalcGrandTotal(rb.TotalFare, rb.PassengerFareItems, request);

        // RecommendationBox BrandedFareItems → FarePackages + DefaultBrandedFareItemId
        var farePackages = MapBrandedFarePackages(rb.BrandedFareItems, rbCurrency, rb.ServiceFee, request);
        var defaultBrandedFareItemId = farePackages.FirstOrDefault(p => p.IsDefault)?.BrandedFareItemId;

        if (!isReturnLeg)
        {
            logger?.LogInformation(
                "[MapRecommendationFlight] ProductId={ProductId}, FarePackages={Count}, Default={DefaultId}",
                rb.ProductId, farePackages.Count, defaultBrandedFareItemId ?? "(null)");
        }

        return new FlightResultDto
        {
            ProductId = productId,
            ProductItemId = rb.ProductId, // her iki bacak için de asıl ID

            AirlineCode = airlineCode,
            AirlineName = airlineName,
            FlightNumber = firstSeg.FlightNumber,

            OriginCode = originCode,
            OriginName = FlightMappings.GetAirportName(originCode),
            DestinationCode = destinationCode,
            DestinationName = FlightMappings.GetAirportName(destinationCode),

            DepartureDate = firstSeg.DepartureDay,
            DepartureTime = firstSeg.DepartureTime,
            ArrivalDate = lastSeg.ArrivalDay,
            ArrivalTime = lastSeg.ArrivalTime,
            DurationHours = totalHours,
            DurationMinutes = totalMinutes,
            DurationFormatted = FormatDuration(totalDurationMinutes),

            Equipment = firstSeg.Equipment,

            // Fiyat: RecommendationBox'taki combined fiyat (gidiş+dönüş toplamı), KİŞİ BAŞI.
            // BiletBank komisyonu ServiceFee/TotalFare'a zaten dahil — olduğu gibi kullan.
            // GrandTotalFare tüm yolcuların toplamıdır.
            BaseFare = rb.BaseFare,
            Taxes = rb.Taxes,
            ServiceFee = rb.ServiceFee,
            TotalFare = rb.TotalFare,
            GrandTotalFare = rbGrandTotal,
            Currency = rbCurrency,
            TotalFareFormatted = FormatPrice(rb.TotalFare, rbCurrency),
            GrandTotalFareFormatted = FormatPrice(rbGrandTotal, rbCurrency),

            IsRefundable = false,
            IsReservable = true,
            RefundableText = "İade politikası için araştırın",

            FareType = FlightMappings.GetFareTypeName(firstSeg.FareType),
            BookingClass = firstSeg.BookingClass,
            BookingClassName = FlightMappings.GetBookingClassName(firstSeg.BookingClass),
            CabinClass = cabinClass,
            CabinClassName = cabinClassName,

            AvailableSeats = 0,
            AvailableSeatsText = null,

            StopCount = stopCount,
            IsDirect = isDirect,
            StopText = stopText,

            Segments = segmentDtos,

            FarePackages = farePackages,
            DefaultBrandedFareItemId = defaultBrandedFareItemId,
            BaggageInfo = null,
            FreeBaggageAllowances = [],

            // Bundle marker alanları
            IsRoundTripBundle = true,
            IsReturnLeg = isReturnLeg,
            BundleProductId = rb.ProductId,

            DepartureFlightId = isReturnLeg ? null : flight.FlightId,
            ReturnFlightId = isReturnLeg ? flight.FlightId : null,
            SubOptionFlightIds = rb.SubOptionFlightIds.Count > 0 ? rb.SubOptionFlightIds : null,
        };
    }

    private static FlightResultDto MapFlightOption(FlightOption option, SearchRequest request, ILogger? logger)
    {
        var segments = option.Segments;
        var firstSegment = segments.FirstOrDefault();
        var lastSegment = segments.LastOrDefault();

        // Segment DTO'larını oluştur
        var segmentDtos = new List<FlightSegmentDto>();
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            var segDto = MapSegment(seg);

            // Aktarma bekleme süresi (ilk segment hariç)
            if (i > 0)
            {
                var prevSeg = segments[i - 1];
                var layover = CalculateLayoverMinutes(prevSeg, seg);
                if (layover.HasValue)
                {
                    segDto.LayoverMinutes = layover.Value;
                    segDto.LayoverFormatted = FormatDuration(layover.Value);
                }
            }

            segmentDtos.Add(segDto);
        }

        // Toplam süre: ilk segmentin kalkışından son segmentin varışına
        var (totalHours, totalMinutes, totalDurationMinutes) = CalculateTotalDuration(firstSegment, lastSegment);

        // Aktarma bilgisi
        int stopCount = segments.Count - 1;
        bool isDirect = stopCount == 0;
        string stopText = BuildStopText(segments, stopCount, isDirect);

        // Havayolu
        string airlineCode = firstSegment?.MarketingAirline ?? "";
        string airlineName = FlightMappings.GetAirlineName(airlineCode);

        // Güzergah (OD — ilk origin, son destination)
        string originCode = firstSegment?.OriginCode ?? "";
        string destinationCode = lastSegment?.DestinationCode ?? "";

        // Kapasite
        int availableSeats = option.SegmentAvailabilities.FirstOrDefault()?.AvailableSeats ?? 0;
        string availableSeatsText = availableSeats <= 3
            ? $"Son {availableSeats} koltuk!"
            : $"{availableSeats} koltuk kaldı";

        // Komisyon
        var firstPaxFare = option.PassengerFareItems.FirstOrDefault();
        decimal commMin = firstPaxFare?.CustomerCommission?.Minimum ?? 0;
        decimal commMax = firstPaxFare?.CustomerCommission?.Maximum ?? 0;
        decimal commVal = firstPaxFare?.CustomerCommission?.Value ?? 0;

        // Fiyat: BiletBank ServiceFee/TotalFare acente komisyonunu (CustomerCommission.Value) ZATEN içerir
        // (ServiceFee = SystemServiceFee + Komisyon). Komisyon TEKRAR EKLENMEZ. GrandTotalFare yalnızca
        // tüm yolcuların toplamıdır (kişi başı × yolcu sayısı), checkout grandTotal ile birebir aynı.
        var optionCurrency = option.Currency ?? "TRY";
        var grandTotal = CalcGrandTotal(option.TotalFare, option.PassengerFareItems, request);

        // Fiyat doğrulama (loglama) — BB değişmez kuralını kontrol eder.
        ValidatePricing(option, logger);

        // Kabin sınıfı belirleme
        var cabinClass = DetermineCabinClass(firstSegment?.BookingClass, firstSegment?.FareType);
        var cabinClassName = FlightMappings.GetFareTypeName(cabinClass);

        var result = new FlightResultDto
        {
            // Kimlik
            ProductId = option.ProductId,
            ProductItemId = option.ProductItemId,

            // Havayolu
            AirlineCode = airlineCode,
            AirlineName = airlineName,
            FlightNumber = firstSegment?.FlightNumber,
            BookingProvider = option.BookingProvider,

            // Güzergah
            OriginCode = originCode,
            OriginName = FlightMappings.GetAirportName(originCode),
            DestinationCode = destinationCode,
            DestinationName = FlightMappings.GetAirportName(destinationCode),

            // Zaman
            DepartureDate = firstSegment?.DepartureDay,
            DepartureTime = firstSegment?.DepartureTime,
            ArrivalDate = lastSegment?.ArrivalDay,
            ArrivalTime = lastSegment?.ArrivalTime,
            DurationHours = totalHours,
            DurationMinutes = totalMinutes,
            DurationFormatted = FormatDuration(totalDurationMinutes),

            // Uçak
            Equipment = firstSegment?.Equipment,

            // Fiyat (KİŞİ BAŞI) — BiletBank komisyonu ServiceFee/TotalFare'a zaten dahil etmiş, olduğu gibi kullan.
            BaseFare = option.BaseFare,
            Taxes = option.Taxes,
            ServiceFee = option.ServiceFee,
            TotalFare = option.TotalFare,
            GrandTotalFare = grandTotal,
            Currency = optionCurrency,
            TotalFareFormatted = FormatPrice(option.TotalFare, optionCurrency),
            GrandTotalFareFormatted = FormatPrice(grandTotal, optionCurrency),

            // Durum
            IsRefundable = option.IsRefundable,
            IsReservable = option.IsReservable,
            RefundableText = option.IsRefundable ? "İade Edilebilir" : "İade Edilemez",

            // Sınıf
            FareType = FlightMappings.GetFareTypeName(firstSegment?.FareType),
            BookingClass = firstSegment?.BookingClass,
            BookingClassName = FlightMappings.GetBookingClassName(firstSegment?.BookingClass),
            CabinClass = cabinClass,
            CabinClassName = cabinClassName,

            // Kapasite
            AvailableSeats = availableSeats,
            AvailableSeatsText = availableSeatsText,

            // Aktarma
            StopCount = stopCount,
            IsDirect = isDirect,
            StopText = stopText,

            // Segmentler
            Segments = segmentDtos,

            // Komisyon
            CustomerCommissionMin = commMin,
            CustomerCommissionMax = commMax,
            CustomerCommissionValue = commVal,

            // Bagaj ham veri
            FreeBaggageAllowances = option.FreeBaggageAllowances,

            // Paketler (tum branded fare secenekleri)
            FarePackages = MapBrandedFarePackages(option, request),

            // Bagaj özeti
            BaggageInfo = MapBaggageInfo(option.FreeBaggageAllowances)
        };

        // Default (en dusuk fiyatli) paketi isaretle
        var defaultPkg = result.FarePackages.FirstOrDefault(p => p.IsDefault);
        result.DefaultBrandedFareItemId = defaultPkg?.BrandedFareItemId;

        return result;
    }

    private static FlightSegmentDto MapSegment(FlightSegment seg)
    {
        var (durationHours, durationMinutes, totalMin) = CalculateSegmentDuration(seg);

        return new FlightSegmentDto
        {
            SequenceNo = seg.SequenceNo,
            OriginCode = seg.OriginCode,
            OriginName = FlightMappings.GetAirportName(seg.OriginCode),
            DestinationCode = seg.DestinationCode,
            DestinationName = FlightMappings.GetAirportName(seg.DestinationCode),
            DepartureDate = seg.DepartureDay,
            DepartureTime = seg.DepartureTime,
            ArrivalDate = seg.ArrivalDay,
            ArrivalTime = seg.ArrivalTime,
            DurationHours = durationHours,
            DurationMinutes = durationMinutes,
            DurationFormatted = FormatDuration(totalMin),
            AirlineCode = seg.MarketingAirline,
            AirlineName = FlightMappings.GetAirlineName(seg.MarketingAirline),
            FlightNumber = seg.FlightNumber,
            Equipment = seg.Equipment,
            BookingClass = seg.BookingClass,
            BookingClassName = FlightMappings.GetBookingClassName(seg.BookingClass),
            FareType = seg.FareType,
            FareTypeName = FlightMappings.GetFareTypeName(seg.FareType)
        };
    }

    private static (int hours, int minutes, int totalMinutes) CalculateSegmentDuration(FlightSegment seg)
    {
        // Duration zaten parse edilmiş (dakika cinsinden) ise kullan
        if (seg.Duration > 0)
        {
            int h = seg.Duration / 60;
            int m = seg.Duration % 60;
            return (h, m, seg.Duration);
        }

        // Duration 0 ise departure/arrival'dan hesapla
        return CalculateDurationFromTimes(seg.DepartureDay, seg.DepartureTime, seg.ArrivalDay, seg.ArrivalTime);
    }

    private static (int hours, int minutes, int totalMinutes) CalculateTotalDuration(
        FlightSegment? first, FlightSegment? last)
    {
        if (first == null || last == null)
            return (0, 0, 0);

        return CalculateDurationFromTimes(first.DepartureDay, first.DepartureTime, last.ArrivalDay, last.ArrivalTime);
    }

    private static (int hours, int minutes, int totalMinutes) CalculateDurationFromTimes(
        string? departureDay, string? departureTime, string? arrivalDay, string? arrivalTime)
    {
        if (string.IsNullOrEmpty(departureDay) || string.IsNullOrEmpty(departureTime) ||
            string.IsNullOrEmpty(arrivalDay) || string.IsNullOrEmpty(arrivalTime))
            return (0, 0, 0);

        if (DateTime.TryParse($"{departureDay}T{departureTime}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dep) &&
            DateTime.TryParse($"{arrivalDay}T{arrivalTime}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var arr))
        {
            var duration = arr - dep;
            // Gece yarısı geçişinde negatif çıkabilir
            if (duration.TotalMinutes < 0)
                duration = duration.Add(TimeSpan.FromDays(1));

            int totalMin = (int)duration.TotalMinutes;
            return (totalMin / 60, totalMin % 60, totalMin);
        }

        return (0, 0, 0);
    }

    private static int? CalculateLayoverMinutes(FlightSegment prevSeg, FlightSegment nextSeg)
    {
        if (string.IsNullOrEmpty(prevSeg.ArrivalDay) || string.IsNullOrEmpty(prevSeg.ArrivalTime) ||
            string.IsNullOrEmpty(nextSeg.DepartureDay) || string.IsNullOrEmpty(nextSeg.DepartureTime))
            return null;

        if (DateTime.TryParse($"{prevSeg.ArrivalDay}T{prevSeg.ArrivalTime}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var prevArr) &&
            DateTime.TryParse($"{nextSeg.DepartureDay}T{nextSeg.DepartureTime}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var nextDep))
        {
            var layover = nextDep - prevArr;
            if (layover.TotalMinutes < 0)
                layover = layover.Add(TimeSpan.FromDays(1));
            return (int)layover.TotalMinutes;
        }

        return null;
    }

    private static string BuildStopText(List<FlightSegment> segments, int stopCount, bool isDirect)
    {
        if (isDirect)
            return "Aktarmasız";

        // Aktarma şehirlerini bul (ara segment'lerin varış noktaları = aktarma noktaları)
        var stopCities = new List<string>();
        for (int i = 0; i < segments.Count - 1; i++)
        {
            var destCode = segments[i].DestinationCode;
            var cityName = FlightMappings.GetAirportName(destCode);
            // Eğer havalimanı adı bulunamadıysa kodu göster
            stopCities.Add(cityName != destCode ? cityName : destCode ?? "");
        }

        var citiesText = string.Join(", ", stopCities);
        return $"{stopCount} Aktarma ({citiesText})";
    }

    private static string FormatDuration(int totalMinutes)
    {
        if (totalMinutes <= 0) return "0dk";
        int h = totalMinutes / 60;
        int m = totalMinutes % 60;
        if (h == 0) return $"{m}dk";
        if (m == 0) return $"{h}sa";
        return $"{h}sa {m}dk";
    }

    private static string FormatPrice(decimal amount, string currency)
    {
        var formatted = amount.ToString("N2", new CultureInfo("tr-TR"));
        var currencySymbol = currency.ToUpperInvariant() switch
        {
            "TRY" => "TL",
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            _ => currency
        };
        return $"{formatted} {currencySymbol}";
    }

    private static void ValidatePricing(FlightOption option, ILogger? logger)
    {
        if (logger == null) return;

        // BB invariant: BaseFare + Taxes + ServiceFee == TotalFare (ServiceFee toplam içinde dahil).
        var expectedTotal = option.BaseFare + option.Taxes + option.ServiceFee;
        if (Math.Abs(expectedTotal - option.TotalFare) > 1m)
        {
            logger.LogWarning(
                "[PriceValidation] ProductId={ProductId}: BaseFare({BaseFare}) + Taxes({Taxes}) + ServiceFee({ServiceFee}) = {Expected}, TotalFare = {TotalFare}",
                option.ProductId, option.BaseFare, option.Taxes, option.ServiceFee, expectedTotal, option.TotalFare);
        }
    }

    public static FlightFilterOptionsDto BuildFilterOptions(List<FlightResultDto> flights)
    {
        if (flights.Count == 0)
        {
            return new FlightFilterOptionsDto();
        }

        var airlineCodes = flights
            .Where(f => !string.IsNullOrEmpty(f.AirlineCode))
            .Select(f => f.AirlineCode!)
            .Distinct()
            .ToList();

        var airlines = airlineCodes
            .Select(code => new AirlineFilterItem
            {
                Code = code,
                Name = FlightMappings.GetAirlineName(code)
            })
            .ToList();

        var departureTimes = flights
            .Where(f => !string.IsNullOrEmpty(f.DepartureTime))
            .Select(f => f.DepartureTime!)
            .OrderBy(t => t)
            .ToList();

        return new FlightFilterOptionsDto
        {
            MinPrice = flights.Min(f => f.TotalFare),
            MaxPrice = flights.Max(f => f.TotalFare),
            Airlines = airlines,
            HasDirectFlights = flights.Any(f => f.IsDirect),
            HasRefundableFlights = flights.Any(f => f.IsRefundable),
            EarliestDeparture = departureTimes.FirstOrDefault(),
            LatestDeparture = departureTimes.LastOrDefault(),
            CabinClasses = flights
                .Where(f => !string.IsNullOrEmpty(f.CabinClassName))
                .Select(f => f.CabinClassName!)
                .Distinct()
                .ToList(),
            FarePackages = flights
                .SelectMany(f => f.FarePackages)
                .Where(p => !string.IsNullOrEmpty(p.BrandName))
                .Select(p => p.BrandName!)
                .Distinct()
                .ToList()
        };
    }

    private static string DetermineCabinClass(string? bookingClass, string? fareType)
    {
        if (!string.IsNullOrEmpty(fareType))
        {
            var ft = fareType.ToUpperInvariant();
            if (ft is "ECO" or "ECONOMY") return "Economy";
            if (ft is "BUS" or "BUSINESS") return "Business";
            if (ft is "FIR" or "FIRST") return "First";
            if (ft is "PEF" or "PREMIUMECONOMY") return "PremiumEconomy";
        }

        if (string.IsNullOrEmpty(bookingClass)) return "Economy";

        return bookingClass.ToUpperInvariant() switch
        {
            "F" or "A" or "P" => "First",
            "C" or "D" or "J" or "Z" or "I" => "Business",
            "R" => "PremiumEconomy",
            _ => "Economy"
        };
    }

    private static List<BrandedFareOptionDto> MapBrandedFarePackages(FlightOption option, SearchRequest request)
    {
        return MapBrandedFarePackages(option.BrandedFareItems, option.Currency ?? "TRY", option.ServiceFee, request);
    }

    private static List<BrandedFareOptionDto> MapBrandedFarePackages(
        List<BrandedFareItem> brandedFareItems, string currency, decimal serviceFee, SearchRequest request)
    {
        var packages = new List<BrandedFareOptionDto>();

        if (brandedFareItems.Count == 0)
            return packages;

        // BB BrandedFareItem.TotalFareInfo.TotalFare ServiceFee dahil DEĞİL — sadece BaseFare+Taxes.
        // Listede gösterilecek müşteri fiyatı için parent option/rb'nin ServiceFee'sini eklemek gerek
        // (ServiceFee BB tarafından acente komisyonunu ZATEN içerir — ayrıca markup EKLENMEZ).
        // Fiyatlar KİŞİ BAŞI; "kişi için toplam" için kişi başı × toplam yolcu sayısı kullanılır.
        var totalPax = Math.Max(request.AdultCount + request.ChildCount + request.InfantCount, 1);

        // En dusuk fiyatli paketin toplam fiyatini bul (fark hesabi icin)
        var minTotalFare = brandedFareItems
            .Select(b => (b.TotalFareInfo?.TotalFare ?? decimal.MaxValue) + serviceFee)
            .Min();

        // Tum paketleri fiyata gore sirala ve dondur
        var sortedItems = brandedFareItems
            .OrderBy(b => b.TotalFareInfo?.TotalFare ?? decimal.MaxValue)
            .ToList();

        bool defaultMarked = false;

        foreach (var bfi in sortedItems)
        {
            var firstPax = bfi.BrandedFarePassengers.FirstOrDefault();
            var firstComponent = firstPax?.FareComponents.FirstOrDefault();
            var itemCurrency = firstPax?.PassengerFareInfo?.Currency ?? currency;
            var totalFare = (bfi.TotalFareInfo?.TotalFare ?? 0) + serviceFee;
            var priceDiff = totalFare - minTotalFare;
            var grandTotalFare = totalFare * totalPax;
            var grandPriceDiff = priceDiff * totalPax;
            var isDefault = !defaultMarked;

            if (isDefault)
                defaultMarked = true;

            var package = new BrandedFareOptionDto
            {
                BrandedFareItemId = bfi.BrandedFareItemId,
                TotalFare = totalFare,
                GrandTotalFare = grandTotalFare,
                TotalTaxes = bfi.TotalFareInfo?.TotalTaxes ?? 0,
                CabinClass = firstComponent?.CabinClass,
                BookingClass = firstComponent?.BookingClass,
                Currency = itemCurrency,
                TotalFareFormatted = FormatPrice(totalFare, itemCurrency),
                GrandTotalFareFormatted = FormatPrice(grandTotalFare, itemCurrency),
                PriceDifference = priceDiff,
                PriceDifferenceFormatted = priceDiff == 0 ? null : $"+{FormatPrice(priceDiff, itemCurrency)}",
                GrandPriceDifference = grandPriceDiff,
                GrandPriceDifferenceFormatted = grandPriceDiff == 0 ? null : $"+{FormatPrice(grandPriceDiff, itemCurrency)}",
                IsDefault = isDefault
            };

            // BrandedItem'i BrandId uzerinden esleştir (paket adi, kurallar)
            var brandId = firstComponent?.BrandId;
            var matchedBrandedItem = bfi.BrandedItems
                .FirstOrDefault(bi => bi.BrandId == brandId)
                ?? bfi.BrandedItems.FirstOrDefault();

            if (matchedBrandedItem != null)
            {
                package.BrandCode = matchedBrandedItem.BrandCode;
                package.BrandName = matchedBrandedItem.BrandName;

                package.Rules = matchedBrandedItem.BrandedRules.Select(r => new BrandedRuleDto
                {
                    Description = r.RuleDescription,
                    IsIncluded = r.Application is "F" or "C",
                    IsChargeable = r.Application == "C",
                    ServiceGroup = r.ServiceGroup,
                    Application = r.Application
                }).ToList();
            }

            // Yolcu bazli fiyat kirilimi
            foreach (var pax in bfi.BrandedFarePassengers)
            {
                package.PassengerFares.Add(new PassengerFareBreakdownDto
                {
                    PassengerType = pax.PassengerType,
                    PassengerCount = pax.PassengerCount,
                    BaseFare = pax.PassengerFareInfo?.BaseFare ?? 0,
                    Taxes = pax.PassengerFareInfo?.Taxes ?? 0,
                    TotalFare = pax.PassengerFareInfo?.TotalFare ?? 0,
                    Currency = pax.PassengerFareInfo?.Currency ?? itemCurrency
                });
            }

            packages.Add(package);
        }

        return packages;
    }

    private static BaggageInfoDto? MapBaggageInfo(List<FreeBaggageAllowance> allowances)
    {
        var baggage = allowances
            .Where(b => b.PaxType is null or "ADT")
            .FirstOrDefault(b => b.Category == "Checked")
            ?? allowances.FirstOrDefault();

        if (baggage == null) return null;

        var unit = baggage.Unit switch
        {
            "K" => "kg",
            "N" => "Parça",
            _ => baggage.Unit ?? ""
        };

        return new BaggageInfoDto
        {
            Allowance = baggage.Allowance,
            Unit = unit,
            Category = baggage.Category,
            DisplayText = $"{baggage.Allowance} {unit}"
        };
    }
}
