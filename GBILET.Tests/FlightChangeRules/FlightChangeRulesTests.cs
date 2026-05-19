using FluentAssertions;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Services.FlightChangeRules;
using Xunit;

namespace GBILET.Tests.FlightChangeRules;

public class FlightChangeRulesTests
{
    private static SearchRequest BuildCriteria(int adult = 1, int child = 0, int infant = 0)
        => new() { Origin = "ADB", Destination = "SAW", DepartureDate = new DateTime(2026, 5, 10), AdultCount = adult, ChildCount = child, InfantCount = infant };

    private static FlightResultDto BuildFlight(decimal price = 2500, int seats = 9, bool reservable = true,
        string depDate = "2026-05-10", string depTime = "08:00")
        => new()
        {
            ProductId = "P1",
            TotalFare = price,
            Currency = "TRY",
            AvailableSeats = seats,
            IsReservable = reservable,
            DepartureDate = depDate,
            DepartureTime = depTime
        };

    [Fact]
    public async Task PriceIncreasedRule_returns_PriceIncreased_when_fresh_higher()
    {
        var rule = new PriceIncreasedRule();
        var cached = BuildFlight(price: 2500);
        var fresh = BuildFlight(price: 2800);

        var result = await rule.EvaluateAsync(cached, fresh, BuildCriteria());

        result.Should().NotBeNull();
        result!.Type.Should().Be(FlightChangeType.PriceIncreased);
        result.CanContinue.Should().BeTrue();
        result.HasChanges.Should().BeTrue();
        result.OldPrice.Should().Be(2500);
        result.NewPrice.Should().Be(2800);
    }

    [Fact]
    public async Task PriceIncreasedRule_ignores_sub_cent_difference()
    {
        var rule = new PriceIncreasedRule();
        var cached = BuildFlight(price: 2500.00m);
        var fresh = BuildFlight(price: 2500.005m);

        var result = await rule.EvaluateAsync(cached, fresh, BuildCriteria());

        result.Should().BeNull();
    }

    [Fact]
    public async Task PriceDecreasedRule_returns_PriceDecreased_when_fresh_lower()
    {
        var rule = new PriceDecreasedRule();
        var result = await rule.EvaluateAsync(BuildFlight(price: 2500), BuildFlight(price: 2200), BuildCriteria());

        result.Should().NotBeNull();
        result!.Type.Should().Be(FlightChangeType.PriceDecreased);
        result.CanContinue.Should().BeTrue();
    }

    [Fact]
    public async Task SoldOutRule_blocks_when_seats_zero()
    {
        var rule = new SoldOutRule();
        var fresh = BuildFlight(seats: 0);

        var result = await rule.EvaluateAsync(BuildFlight(), fresh, BuildCriteria());

        result.Should().NotBeNull();
        result!.Type.Should().Be(FlightChangeType.SoldOut);
        result.CanContinue.Should().BeFalse();
    }

    [Fact]
    public async Task SoldOutRule_blocks_when_not_reservable()
    {
        var rule = new SoldOutRule();
        var fresh = BuildFlight(reservable: false);

        var result = await rule.EvaluateAsync(BuildFlight(), fresh, BuildCriteria());

        result.Should().NotBeNull();
        result!.CanContinue.Should().BeFalse();
    }

    [Fact]
    public async Task InsufficientSeatsRule_blocks_when_seats_less_than_pax()
    {
        var rule = new InsufficientSeatsRule();
        var fresh = BuildFlight(seats: 1);

        var result = await rule.EvaluateAsync(BuildFlight(), fresh, BuildCriteria(adult: 3));

        result.Should().NotBeNull();
        result!.Type.Should().Be(FlightChangeType.InsufficientSeats);
        result.CanContinue.Should().BeFalse();
    }

    [Fact]
    public async Task DepartureDayChangedRule_blocks_when_day_differs()
    {
        var rule = new DepartureDayChangedRule();
        var cached = BuildFlight(depDate: "2026-05-10");
        var fresh = BuildFlight(depDate: "2026-05-11");

        var result = await rule.EvaluateAsync(cached, fresh, BuildCriteria());

        result.Should().NotBeNull();
        result!.Type.Should().Be(FlightChangeType.DepartureDayChanged);
        result.CanContinue.Should().BeFalse();
    }

    [Fact]
    public async Task DepartureTimeChangedRule_warns_when_time_differs()
    {
        var rule = new DepartureTimeChangedRule();
        var cached = BuildFlight(depTime: "08:00");
        var fresh = BuildFlight(depTime: "09:30");

        var result = await rule.EvaluateAsync(cached, fresh, BuildCriteria());

        result.Should().NotBeNull();
        result!.Type.Should().Be(FlightChangeType.DepartureTimeChanged);
        result.CanContinue.Should().BeTrue();
        result.OldDepartureTime.Should().Be("08:00");
        result.NewDepartureTime.Should().Be("09:30");
    }

    [Fact]
    public async Task FlightNotFoundRule_blocks_when_fresh_null()
    {
        var rule = new FlightNotFoundRule();
        var result = await rule.EvaluateAsync(BuildFlight(), null, BuildCriteria());

        result.Should().NotBeNull();
        result!.Type.Should().Be(FlightChangeType.NotFound);
        result.CanContinue.Should().BeFalse();
    }

    [Fact]
    public async Task NoChangeRule_always_returns_NoChange()
    {
        var rule = new NoChangeRule();
        var result = await rule.EvaluateAsync(BuildFlight(), BuildFlight(), BuildCriteria());

        result.Should().NotBeNull();
        result!.Type.Should().Be(FlightChangeType.NoChange);
        result.HasChanges.Should().BeFalse();
        result.CanContinue.Should().BeTrue();
    }
}
