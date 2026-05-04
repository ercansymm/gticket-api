using FluentAssertions;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;
using Xunit;

namespace GBILET.Tests.KeyGeneration;

public class FlightSearchKeyGeneratorTests
{
    [Fact]
    public void Build_produces_deterministic_key_with_normalization()
    {
        var req1 = new SearchRequest
        {
            Origin = "adb",
            Destination = " saw ",
            DepartureDate = new DateTime(2026, 5, 10),
            AdultCount = 2,
            ChildCount = 1,
            InfantCount = 0,
            FlightClass = "economy"
        };
        var req2 = new SearchRequest
        {
            Origin = "ADB",
            Destination = "SAW",
            DepartureDate = new DateTime(2026, 5, 10),
            AdultCount = 2,
            ChildCount = 1,
            InfantCount = 0,
            FlightClass = "Economy"
        };

        FlightSearchKeyGenerator.Build(req1).Should().Be(FlightSearchKeyGenerator.Build(req2));
    }

    [Fact]
    public void Build_uses_OW_marker_for_oneway()
    {
        var req = new SearchRequest
        {
            Origin = "ADB",
            Destination = "SAW",
            DepartureDate = new DateTime(2026, 5, 10),
            AdultCount = 1
        };

        var key = FlightSearchKeyGenerator.Build(req);

        key.Should().Contain("_OW_");
        key.Should().StartWith("flight_search:v1:ADB_SAW_20260510");
    }

    [Fact]
    public void Build_includes_return_date_for_roundtrip()
    {
        var req = new SearchRequest
        {
            Origin = "ADB",
            Destination = "SAW",
            DepartureDate = new DateTime(2026, 5, 10),
            ReturnDate = new DateTime(2026, 5, 15),
            AdultCount = 1
        };

        var key = FlightSearchKeyGenerator.Build(req);

        key.Should().Contain("20260510_20260515");
    }
}
