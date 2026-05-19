namespace GBILET.Core.Models.Flight;

/// <summary>
/// Allocate adiminda cache snapshot ile fresh provider yaniti karsilastirildiginda
/// uretilen sonuc. CanContinue=false ise musteri search'e geri yonlendirilir.
/// </summary>
public class FlightChangeResult
{
    /// <summary>Bir degisiklik tespit edildi mi?</summary>
    public bool HasChanges { get; set; }

    /// <summary>Musteri akisa devam edebilir mi? false ise bloklayici bir degisiklik var.</summary>
    public bool CanContinue { get; set; }

    /// <summary>Tespit edilen degisiklik tipi.</summary>
    public FlightChangeType Type { get; set; }

    /// <summary>Cache'teki eski toplam fiyat.</summary>
    public decimal? OldPrice { get; set; }

    /// <summary>Provider'dan donen yeni toplam fiyat.</summary>
    public decimal? NewPrice { get; set; }

    /// <summary>Cache'teki eski kalkis saati (HH:mm).</summary>
    public string? OldDepartureTime { get; set; }

    /// <summary>Provider'dan donen yeni kalkis saati (HH:mm).</summary>
    public string? NewDepartureTime { get; set; }

    /// <summary>Musteriye gosterilecek Turkce mesaj.</summary>
    public string? UserMessage { get; set; }
}

/// <summary>Tespit edilen ucus degisikligi turleri.</summary>
public enum FlightChangeType
{
    NoChange = 0,
    NotFound = 1,
    SoldOut = 2,
    InsufficientSeats = 3,
    DepartureDayChanged = 4,
    DepartureTimeChanged = 5,
    PriceIncreased = 6,
    PriceDecreased = 7
}
