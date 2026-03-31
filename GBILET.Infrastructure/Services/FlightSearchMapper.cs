using GBILET.Core.Helpers;
using GBILET.Core.Models.Flight;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace GBILET.Infrastructure.Services;

public static class FlightSearchMapper
{
    public static FlightSearchResponseDto MapToDto(
        AirSearchResponse response,
        ILogger? logger = null)
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

        foreach (var option in response.FlightOptions)
        {
            var flight = MapFlightOption(option, logger);
            dto.Flights.Add(flight);
        }

        dto.FilterOptions = BuildFilterOptions(dto.Flights);

        return dto;
    }

    private static FlightResultDto MapFlightOption(FlightOption option, ILogger? logger)
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

        // Fiyat doğrulama (loglama)
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

            // Fiyat
            BaseFare = option.BaseFare,
            Taxes = option.Taxes,
            ServiceFee = option.ServiceFee,
            TotalFare = option.TotalFare,
            Currency = option.Currency ?? "TRY",
            TotalFareFormatted = FormatPrice(option.TotalFare, option.Currency ?? "TRY"),

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

            // Branded / baggage (ham veri)
            BrandedFareItems = option.BrandedFareItems,
            FreeBaggageAllowances = option.FreeBaggageAllowances,

            // Paketler (düzleştirilmiş)
            FarePackages = MapBrandedFarePackages(option),

            // Bagaj özeti
            BaggageInfo = MapBaggageInfo(option.FreeBaggageAllowances)
        };

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

        // baseFare + taxes ? netFare
        if (option.NetFare > 0)
        {
            var expectedNet = option.BaseFare + option.Taxes;
            if (Math.Abs(expectedNet - option.NetFare) > 1m)
            {
                logger.LogWarning(
                    "[PriceValidation] ProductId={ProductId}: BaseFare({BaseFare}) + Taxes({Taxes}) = {Expected}, NetFare = {NetFare}",
                    option.ProductId, option.BaseFare, option.Taxes, expectedNet, option.NetFare);
            }
        }

        // netFare + serviceFee ? totalFare
        var baseForTotal = option.NetFare > 0 ? option.NetFare : (option.BaseFare + option.Taxes);
        var expectedTotal = baseForTotal + option.ServiceFee;
        if (Math.Abs(expectedTotal - option.TotalFare) > 1m)
        {
            logger.LogWarning(
                "[PriceValidation] ProductId={ProductId}: Net/Base+Tax({Base}) + ServiceFee({ServiceFee}) = {Expected}, TotalFare = {TotalFare}",
                option.ProductId, baseForTotal, option.ServiceFee, expectedTotal, option.TotalFare);
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

    private static List<BrandedFareOptionDto> MapBrandedFarePackages(FlightOption option)
    {
        var packages = new List<BrandedFareOptionDto>();

        foreach (var bfi in option.BrandedFareItems)
        {
            var firstPax = bfi.BrandedFarePassengers.FirstOrDefault();
            var firstComponent = firstPax?.FareComponents.FirstOrDefault();

            var package = new BrandedFareOptionDto
            {
                BrandedFareItemId = bfi.BrandedFareItemId,
                TotalFare = bfi.TotalFareInfo?.TotalFare ?? 0,
                TotalTaxes = bfi.TotalFareInfo?.TotalTaxes ?? 0,
                CabinClass = firstComponent?.CabinClass,
                BookingClass = firstComponent?.BookingClass
            };

            // BrandedItem'ı BrandId üzerinden eşleştir
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

            var currency = firstPax?.PassengerFareInfo?.Currency ?? option.Currency ?? "TRY";
            package.Currency = currency;
            package.TotalFareFormatted = FormatPrice(package.TotalFare, currency);

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
