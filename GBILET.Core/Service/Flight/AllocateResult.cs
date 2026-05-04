using GBILET.Core.Models.Flight;

namespace GBILET.Core.Service.Flight;

/// <summary>
/// FlightAllocateService donus tipi: provider'dan gelen taze allocate yaniti +
/// cache snapshot ile karsilastirmadan dogan degisiklik bilgisi.
/// </summary>
public class AllocateResult
{
    /// <summary>Provider'dan donen fresh allocate response (her zaman taze, cache'lenmez).</summary>
    public AllocateResponse Allocate { get; set; } = new();

    /// <summary>Cache snapshot ile fresh fiyat/koltuk karsilastirmasi.</summary>
    public FlightChangeResult Change { get; set; } = new();

    /// <summary>Musteri checkout'a gecebilir mi? Allocate.HasError veya Change.CanContinue=false ise false.</summary>
    public bool CanProceedToCheckout { get; set; }
}
