using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;

namespace GBILET.Core.Interfaces;

/// <summary>
/// Allocate sirasinda cache snapshot'i ile fresh provider yaniti karsilastirip
/// degisiklik tespit eden tek bir kuralin sozlesmesi (Strategy pattern).
/// null donerse rule tetiklenmedi, sirayla bir sonraki rule denenir.
/// </summary>
public interface IFlightChangeRule
{
    /// <summary>Daha dusuk sayilar onceliklidir, orchestrator bu sirayla calistirir.</summary>
    int Order { get; }

    Task<FlightChangeResult?> EvaluateAsync(
        FlightResultDto? cached,
        FlightResultDto? fresh,
        SearchRequest criteria,
        CancellationToken ct = default);
}
