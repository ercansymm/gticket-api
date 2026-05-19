using GBILET.Core.Interfaces;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;
using Microsoft.Extensions.Logging;

namespace GBILET.Infrastructure.Services;

/// <summary>
/// IFlightChangeRule koleksiyonunu Order'a gore sirayla calistiran orchestrator.
/// Ilk non-null result'i geri dondurur. Hicbir rule tetiklenmezse NoChange uretir.
/// </summary>
public class FlightChangeDetector
{
    private readonly IReadOnlyList<IFlightChangeRule> _rules;
    private readonly ILogger<FlightChangeDetector> _logger;

    public FlightChangeDetector(IEnumerable<IFlightChangeRule> rules, ILogger<FlightChangeDetector> logger)
    {
        _rules = rules.OrderBy(r => r.Order).ToList();
        _logger = logger;
    }

    public async Task<FlightChangeResult> DetectAsync(
        FlightResultDto? cached,
        FlightResultDto? fresh,
        SearchRequest criteria,
        CancellationToken ct = default)
    {
        foreach (var rule in _rules)
        {
            var result = await rule.EvaluateAsync(cached, fresh, criteria, ct).ConfigureAwait(false);
            if (result == null) continue;

            if (result.Type != FlightChangeType.NoChange)
            {
                _logger.LogInformation(
                    "Flight change detected: {Type} for {ProductId}",
                    result.Type, fresh?.ProductId ?? cached?.ProductId);
            }

            return result;
        }

        return new FlightChangeResult { HasChanges = false, CanContinue = true, Type = FlightChangeType.NoChange };
    }
}
