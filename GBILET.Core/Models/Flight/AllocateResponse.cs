namespace GBILET.Core.Models.Flight;

public class AllocateResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Shopping dosya ID'si (sonraki adimlarda kullanilir)
    /// </summary>
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Son allocate edilen urun ID'leri
    /// </summary>
    public List<string> LastAllocatedProductIds { get; set; } = [];

    /// <summary>
    /// Fiyat degisti mi?
    /// </summary>
    public bool IsPriceChanged { get; set; }

    /// <summary>
    /// Ucus bilgisi degisti mi?
    /// </summary>
    public bool? IsFlightInfoChanged { get; set; }

    /// <summary>
    /// Dosya para birimi
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Allocate sonrasi donen AirBooking bilgileri
    /// </summary>
    public List<AllocateAirBooking> AirBookings { get; set; } = [];

    /// <summary>
    /// Fiyat ozeti
    /// </summary>
    public AllocatePriceSummary? PriceSummary { get; set; }

    /// <summary>
    /// Rezerve edilebilir mi?
    /// </summary>
    public bool CanBeReserved { get; set; }

    /// <summary>
    /// Musteri bilgileri
    /// </summary>
    public AllocateCustomerInfo? CustomerInfo { get; set; }

    /// <summary>
    /// Komisyon limitleri
    /// </summary>
    public decimal MaxServiceCommission { get; set; }
    public decimal MinServiceCommission { get; set; }

    /// <summary>
    /// Odeme yontemleri
    /// </summary>
    public bool IsCreditCardPaymentEnabled { get; set; }
    public bool IsRunningAccountPaymentEnabled { get; set; }

    /// <summary>
    /// Allocate response'taki yolcu bilgileri (T_Passenger).
    /// Booking asamasinda TempTag degerlerini kullanmak icin gereklidir.
    /// </summary>
    public List<AllocatePassenger> Passengers { get; set; } = [];

    /// <summary>
    /// Oturum bilgileri (sonraki cagrilarda kullanilabilir)
    /// </summary>
    public string? SessionId { get; set; }
    public string? SessionToken { get; set; }

    /// <summary>
    /// Debug: Ham SOAP yaniti (gecici - production'da kaldirilacak)
    /// </summary>
    public string? RawSoapResponse { get; set; }

    /// <summary>
    /// Debug: Parse asamasi bilgisi (gecici)
    /// </summary>
    public string? DebugInfo { get; set; }

    // ======================================================================
    // Cache karsilastirma sonucu (opsiyonel - geriye donuk uyumluluk icin nullable).
    // FlightAllocateService bu alanlari doldurur, ham SOAP servisi doldurmaz.
    // Frontend bu alanlar geldiginde uyari/onay modali gosterebilir.
    // ======================================================================

    /// <summary>Cache snapshot ile fresh fiyat/koltuk karsilastirmasinda degisiklik bulundu mu?</summary>
    public bool? HasChanges { get; set; }

    /// <summary>Musteri checkout'a gecebilir mi? Bloklayici degisiklik varsa false.</summary>
    public bool? CanProceedToCheckout { get; set; }

    /// <summary>Tespit edilen degisiklik tipi (string olarak serialize edilir).</summary>
    public string? ChangeType { get; set; }

    /// <summary>Cache'teki eski toplam fiyat.</summary>
    public decimal? OldPrice { get; set; }

    /// <summary>Provider'dan donen yeni toplam fiyat.</summary>
    public decimal? NewPrice { get; set; }

    /// <summary>Cache'teki eski kalkis saati.</summary>
    public string? OldDepartureTime { get; set; }

    /// <summary>Provider'dan donen yeni kalkis saati.</summary>
    public string? NewDepartureTime { get; set; }

    /// <summary>Musteriye gosterilecek Turkce uyari mesaji (degisiklik bulundugunda).</summary>
    public string? UserMessage { get; set; }
}

public class AllocateAirBooking
{
    public string? ProductId { get; set; }
    public string? PNR { get; set; }
    public string? ProviderId { get; set; }
    public string? Status { get; set; }
    public string? Currency { get; set; }
    public decimal TotalFare { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal NetFare { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal LastSellerCommission { get; set; }
    public bool IsRefundable { get; set; }
    public bool CanBeReserved { get; set; }
    public string? ValidatingCarrier { get; set; }
    public string? FlightType { get; set; }

    /// <summary>
    /// Yolcu bazli fiyat detaylari (T_AirBookingItem)
    /// </summary>
    public List<AllocateBookingItem> BookingItems { get; set; } = [];

    public List<AllocateSegment> Segments { get; set; } = [];

    /// <summary>
    /// Branded fare paketleri (ECO, FLEX, PREMIUM vb.)
    /// </summary>
    public List<AllocateBrandedFareItem> BrandedFareItems { get; set; } = [];
    public List<AllocateBrandedItem> BrandedItems { get; set; } = [];

    /// <summary>
    /// Bagaj haklari
    /// </summary>
    public List<AllocateBaggageAllowance> BaggageAllowances { get; set; } = [];
}

public class AllocateBookingItem
{
    public string? ProductItemId { get; set; }
    public string? Currency { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal TotalFare { get; set; }
    public decimal NetFare { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal SystemServiceFee { get; set; }
    public string? Baggage { get; set; }

    /// <summary>
    /// Yolcu referansi
    /// </summary>
    public string? PaxType { get; set; }
    public int PaxSequenceNo { get; set; }
    public string? PaxReferenceId { get; set; }
}

public class AllocateSegment
{
    public string? SegmentId { get; set; }
    public string? OriginCode { get; set; }
    public string? DestinationCode { get; set; }
    public string? DepartureDay { get; set; }
    public string? DepartureTime { get; set; }
    public string? ArrivalDay { get; set; }
    public string? ArrivalTime { get; set; }
    public string? MarketingAirline { get; set; }
    public string? OperatingAirline { get; set; }
    public string? FlightNumber { get; set; }
    public string? BookingClass { get; set; }
    public string? FareBasis { get; set; }
    public string? Duration { get; set; }
    public string? SelectedBrandedFareItemId { get; set; }
    public int SequenceNo { get; set; }
}

public class AllocateBrandedFareItem
{
    public string? BrandedFareItemId { get; set; }
    public string? Currency { get; set; }
    public decimal TotalFare { get; set; }
    public decimal TotalTaxes { get; set; }

    public List<AllocateBrandedFarePassenger> Passengers { get; set; } = [];
}

public class AllocateBrandedFarePassenger
{
    public string? PassengerType { get; set; }
    public int PassengerCount { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal TotalFare { get; set; }
    public string? Currency { get; set; }
    public string? BookingClass { get; set; }
    public string? CabinClass { get; set; }
    public string? FareBasisCode { get; set; }
    public string? BrandId { get; set; }
    public int SeatsAvailable { get; set; }
}

public class AllocateBrandedItem
{
    public string? BrandId { get; set; }
    public string? BrandCode { get; set; }
    public string? BrandName { get; set; }
    public List<AllocateBrandedRule> Rules { get; set; } = [];
}

public class AllocateBrandedRule
{
    public string? Application { get; set; }
    public string? DisplayType { get; set; }
    public string? RuleDescription { get; set; }
    public string? ServiceGroup { get; set; }
}

public class AllocateBaggageAllowance
{
    public string? Id { get; set; }
    public string? PaxType { get; set; }
    public string? Allowance { get; set; }
    public string? Category { get; set; }
    public string? Type { get; set; }
    public string? Unit { get; set; }
}

public class AllocateCustomerInfo
{
    public string? BusinessId { get; set; }
    public string? BusinessName { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
}

public class AllocatePriceSummary
{
    public decimal GrandTotal { get; set; }
    public decimal TotalBaseFare { get; set; }
    public decimal TotalTaxes { get; set; }
    public decimal TotalServiceFee { get; set; }
    public string? Currency { get; set; }

    /// <summary>
    /// GrandTotal 0 geldiyse T_PriceItem'lardan hesaplanan toplam
    /// </summary>
    public List<AllocatePriceItem> PriceItems { get; set; } = [];
}

public class AllocatePriceItem
{
    public string? ProductId { get; set; }
    public string? ProductType { get; set; }
    public decimal Total { get; set; }
}

public class AllocatePassenger
{
    public string? TempTag { get; set; }
    public int SequenceNo { get; set; }
    public string? Type { get; set; }

    /// <summary>
    /// T_AirBookingItem > PaxReference > PaxReferenceId
    /// TempTag ile eslesme icin kullanilir.
    /// </summary>
    public string? PaxReferenceId { get; set; }
}
