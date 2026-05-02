using GBILET.Core.Interfaces;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;

namespace GBILET.Infrastructure.Services.FlightChangeRules;

/// <summary>Provider yanitinda secilen ucus artik bulunamadi (fresh==null).</summary>
public sealed class FlightNotFoundRule : IFlightChangeRule
{
    public int Order => 10;

    public Task<FlightChangeResult?> EvaluateAsync(FlightResultDto? cached, FlightResultDto? fresh, SearchRequest criteria, CancellationToken ct = default)
    {
        if (fresh != null) return Task.FromResult<FlightChangeResult?>(null);

        return Task.FromResult<FlightChangeResult?>(new FlightChangeResult
        {
            HasChanges = true,
            CanContinue = false,
            Type = FlightChangeType.NotFound,
            UserMessage = "Seçtiğiniz uçuş artık mevcut değil. Lütfen yeni bir arama yapınız."
        });
    }
}

/// <summary>Ucus tamamen tukenmis (IsReservable=false veya AvailableSeats=0).</summary>
public sealed class SoldOutRule : IFlightChangeRule
{
    public int Order => 20;

    public Task<FlightChangeResult?> EvaluateAsync(FlightResultDto? cached, FlightResultDto? fresh, SearchRequest criteria, CancellationToken ct = default)
    {
        if (fresh == null) return Task.FromResult<FlightChangeResult?>(null);
        if (fresh.IsReservable && fresh.AvailableSeats > 0) return Task.FromResult<FlightChangeResult?>(null);

        return Task.FromResult<FlightChangeResult?>(new FlightChangeResult
        {
            HasChanges = true,
            CanContinue = false,
            Type = FlightChangeType.SoldOut,
            UserMessage = "Seçtiğiniz uçuş için koltuk kalmamıştır. Lütfen başka bir uçuş seçiniz."
        });
    }
}

/// <summary>Talep edilen yolcu sayisi icin yeterli koltuk yok.</summary>
public sealed class InsufficientSeatsRule : IFlightChangeRule
{
    public int Order => 30;

    public Task<FlightChangeResult?> EvaluateAsync(FlightResultDto? cached, FlightResultDto? fresh, SearchRequest criteria, CancellationToken ct = default)
    {
        if (fresh == null) return Task.FromResult<FlightChangeResult?>(null);
        var requested = criteria.AdultCount + criteria.ChildCount; // infants ayri koltuk almaz
        if (fresh.AvailableSeats >= requested) return Task.FromResult<FlightChangeResult?>(null);

        return Task.FromResult<FlightChangeResult?>(new FlightChangeResult
        {
            HasChanges = true,
            CanContinue = false,
            Type = FlightChangeType.InsufficientSeats,
            UserMessage = $"Bu uçuşta sadece {fresh.AvailableSeats} koltuk kalmıştır, {requested} yolcu için yer bulunmamaktadır."
        });
    }
}

/// <summary>Kalkis gunu degisti (kritik - bloklayici).</summary>
public sealed class DepartureDayChangedRule : IFlightChangeRule
{
    public int Order => 40;

    public Task<FlightChangeResult?> EvaluateAsync(FlightResultDto? cached, FlightResultDto? fresh, SearchRequest criteria, CancellationToken ct = default)
    {
        if (cached == null || fresh == null) return Task.FromResult<FlightChangeResult?>(null);
        if (string.Equals(cached.DepartureDate, fresh.DepartureDate, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<FlightChangeResult?>(null);

        return Task.FromResult<FlightChangeResult?>(new FlightChangeResult
        {
            HasChanges = true,
            CanContinue = false,
            Type = FlightChangeType.DepartureDayChanged,
            UserMessage = $"Uçuş tarihi değişti: {cached.DepartureDate} → {fresh.DepartureDate}. Lütfen yeni tarihi onaylayınız."
        });
    }
}

/// <summary>Kalkis saati degisti (ayni gun).</summary>
public sealed class DepartureTimeChangedRule : IFlightChangeRule
{
    public int Order => 50;

    public Task<FlightChangeResult?> EvaluateAsync(FlightResultDto? cached, FlightResultDto? fresh, SearchRequest criteria, CancellationToken ct = default)
    {
        if (cached == null || fresh == null) return Task.FromResult<FlightChangeResult?>(null);
        if (string.Equals(cached.DepartureTime, fresh.DepartureTime, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<FlightChangeResult?>(null);

        return Task.FromResult<FlightChangeResult?>(new FlightChangeResult
        {
            HasChanges = true,
            CanContinue = true,
            Type = FlightChangeType.DepartureTimeChanged,
            OldDepartureTime = cached.DepartureTime,
            NewDepartureTime = fresh.DepartureTime,
            UserMessage = $"Kalkış saati değişti: {cached.DepartureTime} → {fresh.DepartureTime}. Devam etmek için onaylayınız."
        });
    }
}

/// <summary>Toplam fiyat artti (0.01 toleransi disinda).</summary>
public sealed class PriceIncreasedRule : IFlightChangeRule
{
    private const decimal Tolerance = 0.01m;
    public int Order => 60;

    public Task<FlightChangeResult?> EvaluateAsync(FlightResultDto? cached, FlightResultDto? fresh, SearchRequest criteria, CancellationToken ct = default)
    {
        if (cached == null || fresh == null) return Task.FromResult<FlightChangeResult?>(null);
        if (fresh.TotalFare <= cached.TotalFare + Tolerance) return Task.FromResult<FlightChangeResult?>(null);

        return Task.FromResult<FlightChangeResult?>(new FlightChangeResult
        {
            HasChanges = true,
            CanContinue = true,
            Type = FlightChangeType.PriceIncreased,
            OldPrice = cached.TotalFare,
            NewPrice = fresh.TotalFare,
            UserMessage = $"Fiyat güncellendi: {cached.TotalFare:N2} → {fresh.TotalFare:N2} {fresh.Currency}. Devam etmek için onaylayınız."
        });
    }
}

/// <summary>Toplam fiyat dustu (sessiz bilgi, devama izinli).</summary>
public sealed class PriceDecreasedRule : IFlightChangeRule
{
    private const decimal Tolerance = 0.01m;
    public int Order => 70;

    public Task<FlightChangeResult?> EvaluateAsync(FlightResultDto? cached, FlightResultDto? fresh, SearchRequest criteria, CancellationToken ct = default)
    {
        if (cached == null || fresh == null) return Task.FromResult<FlightChangeResult?>(null);
        if (fresh.TotalFare >= cached.TotalFare - Tolerance) return Task.FromResult<FlightChangeResult?>(null);

        return Task.FromResult<FlightChangeResult?>(new FlightChangeResult
        {
            HasChanges = true,
            CanContinue = true,
            Type = FlightChangeType.PriceDecreased,
            OldPrice = cached.TotalFare,
            NewPrice = fresh.TotalFare,
            UserMessage = $"İyi haber! Fiyat düştü: {cached.TotalFare:N2} → {fresh.TotalFare:N2} {fresh.Currency}."
        });
    }
}

/// <summary>Hicbir kural tetiklenmediginde fallback.</summary>
public sealed class NoChangeRule : IFlightChangeRule
{
    public int Order => int.MaxValue;

    public Task<FlightChangeResult?> EvaluateAsync(FlightResultDto? cached, FlightResultDto? fresh, SearchRequest criteria, CancellationToken ct = default)
    {
        return Task.FromResult<FlightChangeResult?>(new FlightChangeResult
        {
            HasChanges = false,
            CanContinue = true,
            Type = FlightChangeType.NoChange
        });
    }
}
