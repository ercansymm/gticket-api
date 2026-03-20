# GBILET — Biletbank Uçuþ Rezervasyon API Dökümantasyonu

> **Versiyon:** 1.0  
> **Tarih:** 2025  
> **Backend:** .NET 8 — ASP.NET Core Web API  
> **Frontend:** Next.js (hedeflenen)  
> **Veritabaný:** PostgreSQL (Npgsql)  
> **Biletbank Entegrasyonu:** SOAP/XML over HTTP  

---

## 1. PROJE ÖZETÝ

| Özellik | Deðer |
|---|---|
| **Framework** | .NET 8 — ASP.NET Core Web API |
| **Mimari** | Clean Architecture (4 katman) |
| **Katmanlar** | `GBILET.Api` ? `GBILET.Core` ? `GBILET.Infrastructure` + `GBILET.App` |
| **Biletbank iletiþim** | **SOAP/XML** — `HttpClient` ile SOAP Envelope oluþturulup `POST` ediliyor |
| **Auth (kendi backend)** | **YOK** — Þu an JWT / API Key / Session-based auth yok. Endpoint'ler açýk. |
| **Auth (Biletbank)** | Backend ? Biletbank arasý `Login` SOAP çaðrýsý ile `SessionId` + `SessionToken` alýnýyor |
| **Veritabaný** | PostgreSQL — EF Core (`GTicketDbContext`) |
| **Cache** | `IMemoryCache` — search sonrasý session bilgisi 20 dk cache'leniyor |
| **Loglama** | `ILogger<T>` — SOAP request/response loglanýyor |
| **Retry mekanizmasý** | **YOK** — hata durumunda direkt 500 dönüyor |
| **Middleware** | CORS (localhost:3000, localhost:5173), Swagger, HTTPS Redirection |
| **Interceptor / Filter** | **YOK** |
| **Rate Limiting** | **YOK** |
| **Validation** | Manual if-check'ler (FluentValidation yok) |
| **JSON Serialization** | `System.Text.Json` — camelCase naming policy |
| **CORS izinli origin'ler** | `http://localhost:3000`, `http://localhost:5173` |

### Katman Yapýsý

```
GBILET.Api (Presentation)
  ??? Controllers: FlightController, AirportController, AirlineController, PopularRouteController, AuthController
  
GBILET.Core (Domain / Contracts)
  ??? Entities: Booking, Passenger, Airport, Airline, GuestSession, FlightSegment, Payment, ...
  ??? Models/Flight: SearchRequest, AllocateResponse, FlightSearchDto, ...
  ??? Service: IFlightService, IBookingRepository, IAirportRepository, IAirlineRepository, IPopularRouteRepository
  ??? Helpers: FlightMappings
  
GBILET.Infrastructure (Data Access / External)
  ??? Data: GTicketDbContext, BookingRepository, AirportRepository, AirlineRepository, PopularRouteRepository
  ??? Services: BiletBankFlightService (SOAP client), FlightSearchMapper
  ??? Extensions: XmlExtensions (SOAP XML parse helpers)
  
GBILET.App (ayrý bir Blazor/MVC app — sadece AuthController.cs var, aktif kullanýlmýyor)
```

---

## 2. ENDPOINT KATALOÐU

### Tablo Özeti

| # | Method | Path | Auth | Açýklama | Biletbank Servisi | Durum |
|---|--------|------|------|----------|-------------------|-------|
| 1 | `POST` | `/api/flight/search` | ? | Uçuþ arama (DTO format) | `Login` + `AirSearch` | ? HAZIR |
| 2 | `POST` | `/api/flight/search/raw` | ? | Uçuþ arama (ham Biletbank format) | `Login` + `AirSearch` | ? HAZIR |
| 3 | `POST` | `/api/flight/search/sort` | ? | Uçuþ sonuçlarýný sýralama | — (client-side data) | ? HAZIR |
| 4 | `POST` | `/api/flight/search/filter` | ? | Uçuþ sonuçlarýný filtreleme | — (client-side data) | ? HAZIR |
| 5 | `GET`  | `/api/flight/session/{searchId}` | ? | Cache'lenmiþ session bilgisi | — (cache read) | ? HAZIR |
| 6 | `POST` | `/api/flight/allocate` | ? | Uçuþ tahsis (koltuk ayýrma) | `Allocate` (+ opsiyonel Login+AirSearch) | ? HAZIR |
| 7 | `POST` | `/api/flight/update-passengers` | ? | Yolcu bilgisi güncelleme | `UpdatePassengers` | ? HAZIR |
| 8 | `POST` | `/api/flight/make-prebooking` | ? | Ön rezervasyon oluþturma | `MakePrebooking` | ? HAZIR |
| 9 | `GET`  | `/api/airport` | ? | Tüm havalimanlarý | — (DB) | ? HAZIR |
| 10 | `GET` | `/api/airport/domestic` | ? | Yurtiçi havalimanlarý | — (DB) | ? HAZIR |
| 11 | `GET` | `/api/airport/search?q={query}` | ? | Havalimaný arama | — (DB) | ? HAZIR |
| 12 | `GET` | `/api/airport/{iataCode}` | ? | IATA koduna göre havalimaný | — (DB) | ? HAZIR |
| 13 | `GET` | `/api/airline` | ? | Tüm havayollarý | — (DB) | ? HAZIR |
| 14 | `GET` | `/api/airline/{code}` | ? | Koda göre havayolu | — (DB) | ? HAZIR |
| 15 | `GET` | `/api/popularroute` | ? | Popüler rotalar | — (DB) | ? HAZIR |
| 16 | `GET` | `/api/auth/test` | ? | API saðlýk kontrolü | — | ? HAZIR |
| 17 | — | `RemoveProduct` | — | Ürün kaldýrma | `RemoveProduct` | ? **HENÜZ YOK** |
| 18 | — | `MakePayment` | — | Ödeme yapma | `MakePayment` | ? **HENÜZ YOK** |
| 19 | — | `FinalizeShopping` | — | Alýþveriþ sonlandýrma | `FinalizeShopping` | ? **HENÜZ YOK** |
| 20 | — | `PokeShoppingFile` | — | Durum kontrolü | `PokeShoppingFile` | ? **HENÜZ YOK** |
| 21 | — | `ReadShoppingFile` | — | Bilet okuma | `ReadShoppingFile` | ? **HENÜZ YOK** |
| 22 | — | `Logout` (Biletbank) | — | Oturum kapatma | `Logout` | ? **HENÜZ YOK** |
| 23 | — | User Login/Register | — | Kullanýcý giriþi/kaydý | — | ? **HENÜZ YOK** |

---

## 3. ENDPOINT DETAYLARI

---

### 3.1 — `POST /api/flight/search`
> **Açýklama:** Uçuþ arama (frontend-friendly DTO formatýnda)  
> **Biletbank:** `Login` ? `AirSearch`  
> **Auth gerekli:** Hayýr  

#### Request Body
```json
{
  "origin": "IST",                    // ZORUNLU — IATA kodu
  "destination": "AYT",              // ZORUNLU — IATA kodu
  "originCountryCode": "TR",         // varsayýlan: "TR"
  "destinationCountryCode": "TR",    // varsayýlan: "TR"
  "originIsCity": false,             // varsayýlan: false
  "destinationIsCity": false,        // varsayýlan: false
  "departureDate": "2025-07-15",     // ZORUNLU — yyyy-MM-dd
  "returnDate": "2025-07-20",        // RT ise ZORUNLU, OW ise null
  "flightType": "OW",               // "OW" (tek yön), "RT" (gidiþ-dönüþ), "MP" (çoklu)
  "flightClass": "Economy",          // "Economy", "Business", "First", "Comfort"
  "adultCount": 1,                   // varsayýlan: 1
  "childCount": 0,                   // varsayýlan: 0
  "infantCount": 0,                  // varsayýlan: 0
  "directFlightsOnly": false,        // varsayýlan: false
  "refundablesOnly": false,          // varsayýlan: false
  "searchTimeoutMilliseconds": 0,    // varsayýlan: 0 (Biletbank default)
  "preferredAirlines": ["TK", "PC"], // opsiyonel — IATA havayolu kodlarý
  "searchReason": "SearchAndBook"    // "SearchOnly" veya "SearchAndBook"
}
```

#### Validasyon Kurallarý
| Alan | Kural |
|------|-------|
| `origin` | Zorunlu, boþ olamaz |
| `destination` | Zorunlu, boþ olamaz |
| `departureDate` | Zorunlu, `default(DateTime)` olamaz |
| `returnDate` | `flightType == "RT"` ise zorunlu |
| `adultCount + childCount` | Maksimum 9 (bebek hariç) |
| `infantCount` | `adultCount`'u geçemez |

#### Response — Baþarýlý (200)
```json
{
  "hasError": false,
  "errorMessage": null,
  "searchId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "sessionId": "sess-123456",       // ?? Allocate'te kullanýlacak
  "sessionToken": "token-abcdef",   // ?? Allocate'te kullanýlacak
  "flights": [
    {
      "productId": "guid-product-1",             // ?? Allocate'te kullanýlacak
      "productItemId": "guid-product-item-1",
      "airlineCode": "TK",
      "airlineName": "Türk Hava Yollarý",
      "flightNumber": "TK2134",
      "bookingProvider": "Amadeus",
      "originCode": "IST",
      "originName": "Ýstanbul Havalimaný",
      "destinationCode": "AYT",
      "destinationName": "Antalya Havalimaný",
      "departureDate": "2025-07-15",
      "departureTime": "08:30",
      "arrivalDate": "2025-07-15",
      "arrivalTime": "10:00",
      "durationHours": 1,
      "durationMinutes": 30,
      "durationFormatted": "1s 30dk",
      "equipment": "738",
      "baseFare": 850.00,
      "taxes": 215.00,
      "serviceFee": 0,
      "totalFare": 1065.00,
      "currency": "TRY",
      "totalFareFormatted": "1.065,00 TL",
      "isRefundable": true,
      "isReservable": true,
      "refundableText": "Ýade edilebilir",
      "fareType": "Published",
      "bookingClass": "Y",
      "bookingClassName": "Ekonomi Esnek",
      "availableSeats": 5,
      "availableSeatsText": "5 koltuk kaldý",
      "stopCount": 0,
      "isDirect": true,
      "stopText": "Direkt",
      "segments": [
        {
          "sequenceNo": 1,
          "originCode": "IST",
          "originName": "Ýstanbul Havalimaný",
          "destinationCode": "AYT",
          "destinationName": "Antalya Havalimaný",
          "departureDate": "2025-07-15",
          "departureTime": "08:30",
          "arrivalDate": "2025-07-15",
          "arrivalTime": "10:00",
          "durationHours": 1,
          "durationMinutes": 30,
          "durationFormatted": "1s 30dk",
          "airlineCode": "TK",
          "airlineName": "Türk Hava Yollarý",
          "flightNumber": "TK2134",
          "equipment": "738",
          "bookingClass": "Y",
          "bookingClassName": "Ekonomi Esnek",
          "fareType": "Published",
          "fareTypeName": "Yayýnlanmýþ Tarife",
          "layoverMinutes": null,
          "layoverFormatted": null
        }
      ],
      "customerCommissionMin": 0,
      "customerCommissionMax": 50.00,
      "customerCommissionValue": 25.00,
      "brandedFareItems": [ /* BrandedFareItem nesneleri */ ],
      "freeBaggageAllowances": [
        {
          "allowance": "20",
          "category": "Checked",
          "type": "Weight",
          "unit": "K",
          "paxType": "ADT"
        }
      ]
    }
  ],
  "filterOptions": {
    "minPrice": 450.00,
    "maxPrice": 3200.00,
    "airlines": [
      { "code": "TK", "name": "Türk Hava Yollarý" },
      { "code": "PC", "name": "Pegasus Hava Yollarý" }
    ],
    "hasDirectFlights": true,
    "hasRefundableFlights": true,
    "earliestDeparture": "06:00",
    "latestDeparture": "22:45"
  }
}
```

#### Response — Hata (400)
```json
{ "error": "Origin ve Destination alanlarý zorunludur." }
```

#### Response — Sunucu hatasý (500)
```json
{
  "error": "Login hatasý: Login HTTP 500: ...",
  "inner": "Socket connection refused"
}
```

#### Status Code'lar
| Code | Durum |
|------|-------|
| 200 | Baþarýlý — uçuþ sonuçlarý döner |
| 400 | Validasyon hatasý (eksik alan) |
| 500 | Biletbank API eriþim hatasý veya sunucu hatasý |

---

### 3.2 — `POST /api/flight/search/raw`
> **Açýklama:** Uçuþ arama — Biletbank'ýn ham yanýtýný döner (debug/test amaçlý)  
> **Biletbank:** `Login` ? `AirSearch`  

#### Request Body
Search ile ayný (`SearchRequest`).

#### Response
`AirSearchResponse` nesnesi — Biletbank'ýn `T_FlightOption`, `T_RecommendationBox` yapýsýný direkt yansýtýr. Frontend için deðil, backend debugging için kullanýlýr.

---

### 3.3 — `POST /api/flight/search/sort`
> **Açýklama:** Frontend'den gelen uçuþ listesini sýralama (backend'de sýralama)  
> **Biletbank:** Yok — client-side data  

#### Request Body
```json
{
  "sortBy": "price",       // "price"/"cheapest", "earliest", "latest", "shortest"/"duration"
  "flights": [ /* FlightResultDto dizisi — search response'taki flights */ ]
}
```

#### Response (200)
Sýralanmýþ `FlightResultDto[]` dizisi.

> **Not:** Bu endpoint uçuþ listesini body'de alýyor. Frontend kendi tarafýnda da sýralama yapabilir; backend desteði opsiyoneldir.

---

### 3.4 — `POST /api/flight/search/filter`
> **Açýklama:** Frontend'den gelen uçuþ listesini filtreleme  
> **Biletbank:** Yok — client-side data  

#### Request Body
```json
{
  "directOnly": true,
  "refundableOnly": false,
  "minPrice": 500,
  "maxPrice": 2000,
  "airlineCodes": ["TK", "PC"],
  "departureTimeFrom": "08:00",
  "departureTimeTo": "18:00",
  "flights": [ /* FlightResultDto dizisi */ ]
}
```

#### Response (200)
```json
{
  "flights": [ /* Filtrelenmiþ FlightResultDto[] */ ],
  "filterOptions": {
    "minPrice": 500,
    "maxPrice": 1800,
    "airlines": [ { "code": "TK", "name": "Türk Hava Yollarý" } ],
    "hasDirectFlights": true,
    "hasRefundableFlights": false,
    "earliestDeparture": "09:15",
    "latestDeparture": "17:30"
  }
}
```

> **Not:** Ayný sort gibi — tüm flights body'de gönderiliyor. Büyük veri setlerinde performans etkisi olabilir.

---

### 3.5 — `GET /api/flight/session/{searchId}`
> **Açýklama:** Search sonrasý cache'lenen session bilgisini getirir  
> **Cache süresi:** 20 dakika  

#### Response (200)
```json
{
  "searchId": "a1b2c3d4-...",
  "shoppingFileId": "guid-shopping-file",
  "sessionId": "sess-123456",
  "sessionToken": "token-abcdef"
}
```

#### Response (404)
```json
{ "error": "Session bulunamadý veya süresi dolmuþ." }
```

---

### 3.6 — `POST /api/flight/allocate`
> **Açýklama:** Seçilen uçuþ için koltuk tahsisi (Biletbank'ta fiyat ve uygunluk kontrolü)  
> **Biletbank:** `Allocate` (+ opsiyonel `Login` + `AirSearch`)  

#### Request Body
```json
{
  "sessionId": "sess-123456",           // Search'ten gelen (opsiyonel — yoksa searchRequest ile login+search yapýlýr)
  "sessionToken": "token-abcdef",       // Search'ten gelen (opsiyonel)
  "searchRequest": null,                // sessionId yoksa ZORUNLU — SearchRequest nesnesi
  "productId": "guid-product-1",        // ZORUNLU — Search sonucundan seçilen FlightOption.ProductId
  "selectedServiceFee": 0               // Komisyon tutarý (varsayýlan 0)
}
```

#### Validasyon Kurallarý
| Alan | Kural |
|------|-------|
| `productId` | Zorunlu |
| `sessionId` + `sessionToken` | Ýkisi birlikte verilmeli VEYA `searchRequest` dolu olmalý |

#### Response — Baþarýlý (200)
```json
{
  "hasError": false,
  "errorMessage": null,
  "shoppingFileId": "guid-shopping-file",          // ?? Sonraki adýmlarda kullanýlacak
  "lastAllocatedProductIds": ["guid-product-1"],
  "isPriceChanged": false,
  "isFlightInfoChanged": false,
  "currency": "TRY",
  "canBeReserved": true,                           // ?? Dallanma noktasý
  "isCreditCardPaymentEnabled": true,
  "isRunningAccountPaymentEnabled": false,
  "maxServiceCommission": 100.00,
  "minServiceCommission": 0,
  "customerInfo": {
    "businessId": "BIZ-001",
    "businessName": "GTicket",
    "email": "info@gticket.com",
    "username": "gticket_user"
  },
  "airBookings": [
    {
      "productId": "guid-product-1",               // ?? UpdatePassengers ve MakePreBooking'de kullanýlacak
      "pnr": null,
      "providerId": "AMADEUS",
      "status": "Allocated",
      "currency": "TRY",
      "totalFare": 1065.00,
      "baseFare": 850.00,
      "taxes": 215.00,
      "netFare": 1040.00,
      "serviceFee": 0,
      "lastSellerCommission": 0,
      "isRefundable": true,
      "canBeReserved": true,
      "validatingCarrier": "TK",
      "flightType": "OW",
      "bookingItems": [
        {
          "productItemId": "guid-item-1",          // ?? UpdatePassengers'da kullanýlacak
          "currency": "TRY",
          "baseFare": 850.00,
          "taxes": 215.00,
          "totalFare": 1065.00,
          "netFare": 1040.00,
          "serviceFee": 0,
          "systemServiceFee": 25.00,
          "baggage": "20K",
          "paxType": "ADT",
          "paxSequenceNo": 1,
          "paxReferenceId": "pax-ref-guid-1"       // ?? UpdatePassengers'da TempTag olarak kullanýlacak
        }
      ],
      "segments": [
        {
          "segmentId": "seg-guid-1",
          "originCode": "IST",
          "destinationCode": "AYT",
          "departureDay": "2025-07-15T00:00:00",
          "departureTime": "PT8H30M",
          "arrivalDay": "2025-07-15T00:00:00",
          "arrivalTime": "PT10H0M",
          "marketingAirline": "TK",
          "operatingAirline": "TK",
          "flightNumber": "2134",
          "bookingClass": "Y",
          "fareBasis": "YOW",
          "duration": "PT1H30M",
          "selectedBrandedFareItemId": "branded-fare-guid",
          "sequenceNo": 1
        }
      ],
      "brandedFareItems": [
        {
          "brandedFareItemId": "branded-fare-guid", // ?? MakePreBooking'de kullanýlacak
          "currency": "TRY",
          "totalFare": 1065.00,
          "totalTaxes": 215.00,
          "passengers": [
            {
              "passengerType": "ADT",
              "passengerCount": 1,
              "baseFare": 850.00,
              "taxes": 215.00,
              "totalFare": 1065.00,
              "currency": "TRY",
              "bookingClass": "Y",
              "cabinClass": "Economy",
              "fareBasisCode": "YOW",
              "brandId": "brand-eco-id",
              "seatsAvailable": 5
            }
          ]
        }
      ],
      "brandedItems": [
        {
          "brandId": "brand-eco-id",
          "brandCode": "ECO",
          "brandName": "EcoFly",
          "rules": [
            { "application": "F", "displayType": "1", "ruleDescription": "20 kg bagaj dahil", "serviceGroup": "BG" },
            { "application": "C", "displayType": "2", "ruleDescription": "Ücretli koltuk seçimi", "serviceGroup": "SA" },
            { "application": "N", "displayType": "3", "ruleDescription": "Mil kazanýmý yok", "serviceGroup": "ML" }
          ]
        }
      ],
      "baggageAllowances": [
        {
          "id": "fba-guid-1",
          "paxType": "ADT",
          "allowance": "20",
          "category": "Checked",
          "type": "Weight",
          "unit": "K"
        }
      ]
    }
  ],
  "priceSummary": {
    "grandTotal": 1065.00,
    "totalBaseFare": 850.00,
    "totalTaxes": 215.00,
    "totalServiceFee": 0,
    "currency": "TRY",
    "priceItems": [
      { "productId": "guid-product-1", "productType": "Air", "total": 1065.00 }
    ]
  },
  "passengers": [
    {
      "tempTag": "pax-ref-guid-1",                 // ?? UpdatePassengers'da gönderilecek
      "sequenceNo": 1,
      "type": "ADT",
      "paxReferenceId": "pax-ref-guid-1"           // ?? UpdatePassengers'da gönderilecek
    }
  ],
  "sessionId": "sess-123456",
  "sessionToken": "token-abcdef",
  "rawSoapResponse": "<!-- debug: ham XML -->",     // ?? DEBUG — production'da kaldýrýlacak
  "debugInfo": "Elements found: ..."                // ?? DEBUG — production'da kaldýrýlacak
}
```

---

### 3.7 — `POST /api/flight/update-passengers`
> **Açýklama:** Yolcu bilgilerini Biletbank'a gönderir  
> **Biletbank:** `UpdatePassengers`  

#### Request Body
```json
{
  "sessionId": "sess-123456",           // ZORUNLU — Allocate'ten
  "sessionToken": "token-abcdef",       // ZORUNLU — Allocate'ten
  "shoppingFileId": "guid-shopping-file", // ZORUNLU — Allocate'ten
  "productId": "guid-product-1",        // ZORUNLU — Allocate'ten airBookings[0].productId
  "productItemId": "guid-item-1",       // ZORUNLU — Allocate'ten bookingItems[0].productItemId
  "passengers": [
    {
      "paxType": "ADT",                 // "ADT", "CHD", "INF"
      "sequenceNo": 1,                  // Allocate'teki SequenceNo ile eþleþmeli
      "firstName": "MEHMET",            // ZORUNLU
      "lastName": "YILMAZ",             // ZORUNLU
      "gender": "M",                    // ZORUNLU — "M" veya "F"
      "birthDate": "1990-05-15",        // ZORUNLU — yyyy-MM-dd
      "citizenNo": "12345678901",       // Opsiyonel — 11 hane TC Kimlik No
      "passportNo": null,               // Opsiyonel — uluslararasý uçuþlarda zorunlu olabilir
      "passportCountry": null,          // Opsiyonel
      "nationality": "TR",             // Varsayýlan: "TR"
      "tempTag": "pax-ref-guid-1",      // Allocate'ten gelen TempTag veya PaxReferenceId
      "paxReferenceId": "pax-ref-guid-1" // ZORUNLU — Allocate'ten
    }
  ],
  "contact": {
    "email": "mehmet@example.com",       // ZORUNLU
    "phone": "+905551234567"             // ZORUNLU
  }
}
```

#### Validasyon Kurallarý
| Alan | Kural |
|------|-------|
| `sessionId`, `sessionToken` | Zorunlu |
| `shoppingFileId` | Zorunlu |
| `productId`, `productItemId` | Zorunlu |
| `passengers` | En az 1 yolcu |
| Her yolcu `firstName`, `lastName` | Zorunlu |
| Her yolcu `birthDate` | Zorunlu |
| Her yolcu `gender` | Zorunlu — "M" veya "F" |
| Her yolcu `paxReferenceId` | Zorunlu |
| `contact.email`, `contact.phone` | Zorunlu |

#### Response — Baþarýlý (200)
```json
{
  "hasError": false,
  "errorMessage": null
}
```

#### Response — Biletbank Hatasý (200 ama hasError=true)
```json
{
  "hasError": true,
  "errorMessage": "TempTag eþleþmesi bulunamadý."
}
```

---

### 3.8 — `POST /api/flight/make-prebooking`
> **Açýklama:** Ön rezervasyon oluþturur. Baþarýlý olursa PNR kodu döner ve DB'ye booking kaydý oluþturulur.  
> **Biletbank:** `MakePrebooking`  

#### Request Body
```json
{
  "sessionId": "sess-123456",                // ZORUNLU — Allocate'ten
  "sessionToken": "token-abcdef",            // ZORUNLU — Allocate'ten
  "productId": "guid-product-1",             // ZORUNLU — Allocate'ten airBookings[0].productId
  "brandedFareItemId": "branded-fare-guid",  // ZORUNLU — Allocate'ten airBookings[0].brandedFareItems[0].brandedFareItemId
  "shoppingFileId": "guid-shopping-file",    // ZORUNLU — Allocate'ten
  "userId": null,                            // Opsiyonel — kayýtlý kullanýcý ID'si (null ise misafir oturumu oluþturulur)
  "passengers": [                            // ZORUNLU — DB kaydý için
    {
      "paxType": "ADT",
      "sequenceNo": 1,
      "firstName": "MEHMET",
      "lastName": "YILMAZ",
      "gender": "M",
      "birthDate": "1990-05-15",
      "citizenNo": "12345678901",
      "passportNo": null,
      "passportCountry": null,
      "nationality": "TR",
      "tempTag": "pax-ref-guid-1",
      "paxReferenceId": "pax-ref-guid-1"
    }
  ],
  "contact": {                               // ZORUNLU — DB kaydý ve misafir session oluþturma için
    "email": "mehmet@example.com",
    "phone": "+905551234567"
  }
}
```

#### Response — Baþarýlý (200)
```json
{
  "hasError": false,
  "errorMessage": null,
  "bookingCode": "ABC123",                  // PNR kodu
  "status": "Reserved",                     // "Reserved", "PreBooked", "Confirmed"
  "totalFare": 1065.00,
  "baseFare": 850.00,
  "taxes": 215.00,
  "serviceFee": 0,
  "currency": "TRY",
  "shoppingFileId": "guid-shopping-file",
  "isPriceChanged": false,
  "prebookingExpiresAt": "2025-07-14T23:59:00Z",
  "reservationExpiresAt": "2025-07-15T08:00:00Z",
  "segments": [
    {
      "segmentId": "seg-guid-1",
      "originCode": "IST",
      "destinationCode": "AYT",
      "departureDay": "2025-07-15",
      "departureTime": "08:30",
      "arrivalDay": "2025-07-15",
      "arrivalTime": "10:00",
      "flightNumber": "2134",
      "marketingAirline": "TK",
      "bookingClass": "Y"
    }
  ],
  "passengers": [
    {
      "firstName": "MEHMET",
      "lastName": "YILMAZ",
      "type": "ADT",
      "citizenNo": "12345678901",
      "gender": "M"
    }
  ],
  "bookingId": "guid-db-booking-id",         // Backend DB'de oluþturulan booking kaydý
  "userId": null,                            // Kayýtlý kullanýcý ID'si (null ise misafir)
  "guestSessionId": "guid-guest-session",    // Misafir oturum ID'si
  "isGuest": true                            // Misafir mi?
}
```

---

### 3.9–3.16 — Yardýmcý Endpoint'ler

#### `GET /api/airport` — Tüm havalimanlarý
```json
[
  {
    "id": 1,
    "iataCode": "IST",
    "icaoCode": "LTFM",
    "nameTr": "Ýstanbul Havalimaný",
    "nameEn": "Istanbul Airport",
    "cityTr": "Ýstanbul",
    "cityEn": "Istanbul",
    "countryTr": "Türkiye",
    "countryEn": "Turkey",
    "countryCode": "TR",
    "timezone": "Europe/Istanbul",
    "isCity": false,
    "isDomestic": true,
    "isActive": true,
    "sortOrder": 1
  }
]
```

#### `GET /api/airport/domestic` — Sadece yurtiçi
Ayný format, `isDomestic == true` filtreli.

#### `GET /api/airport/search?q=ista` — Arama
`q` parametresi min 2 karakter. Ayný Airport nesnesi dizisi döner.

#### `GET /api/airport/{iataCode}` — Tekil
Tek Airport nesnesi. 404 ise: `{ "error": "'XYZ' kodlu havalimaný bulunamadý." }`

#### `GET /api/airline` — Tüm havayollarý
```json
[
  {
    "id": 1,
    "code": "TK",
    "nameTr": "Türk Hava Yollarý",
    "nameEn": "Turkish Airlines",
    "logoUrl": null,
    "isActive": true
  }
]
```

#### `GET /api/airline/{code}` — Tekil havayolu

#### `GET /api/popularroute` — Popüler rotalar

#### `GET /api/auth/test` — Saðlýk kontrolü
```json
"API çalýþýyor"
```

---

## 4. SESSION YÖNETÝMÝ

### 4.1 — Session Üretimi

| Deðer | Üreten | Nasýl? |
|-------|--------|--------|
| `SessionId` | **Biletbank** | `Login` SOAP response'ýndan gelir |
| `SessionToken` | **Biletbank** | `Login` SOAP response'ýndan gelir |
| `ShoppingFileId` | **Biletbank** | `AirSearch` response'ýndan gelir, `Allocate`'te de döner |
| `SearchId` | **Biletbank** | `AirSearch` response'ýndan gelir |

### 4.2 — Session Taþýma

```
Search ? SessionId + SessionToken (response body'de)
   ?
Allocate ? Request body'de gönderilir
   ?
UpdatePassengers ? Request body'de gönderilir  
   ?
MakePreBooking ? Request body'de gönderilir
```

> **Taþýma yöntemi:** Tüm session bilgileri **request/response body** içinde taþýnýyor. Header, cookie, token yok.

### 4.3 — Session Süresi

| Cache | Süre |
|-------|------|
| Backend MemoryCache (SearchId ? SessionData) | **20 dakika** |
| Biletbank oturumu | Biletbank'ýn belirlediði süre (genellikle **20–30 dakika**) |

> Timeout olduðunda: Search'ten dönen session ile Allocate çaðrýlýrsa Biletbank hata döner. Frontend'in yeni search yapmasý gerekir.

### 4.4 — Misafir vs Üye Session

| Senaryo | UserId | GuestSessionId | Nasýl oluþuyor? |
|---------|--------|----------------|-----------------|
| Kayýtlý kullanýcý | ? dolu | null | `MakePreBooking` request'inde `userId` verilir |
| Misafir | null | ? dolu | Backend, `contact.email` ile `GuestSession` oluþturur/bulur |

> **Misafir booking yapabilir mi?** ? Evet. `userId` null gönderilirse backend otomatik olarak `GuestSession` oluþturur.

### 4.5 — shoppingFileId Yaþam Döngüsü

```
AirSearch ? shoppingFileId oluþur (Biletbank tarafýnda)
   ?
Allocate ? response'ta döner (ayný veya güncellenmiþ)
   ?
UpdatePassengers ? request body'de gönderilir (validasyon)
   ?
MakePreBooking ? request body'de gönderilir (ExtraParam: IntendedShoppingFileId)
   ?
[MakePayment] ? HENÜZ YOK — burada da kullanýlacak
   ?
[FinalizeShopping] ? HENÜZ YOK — burada da kullanýlacak
```

---

## 5. BOOKING AKIÞI (ADIM ADIM)

```
???????????????????????????????????????????????????????????????????
?                    GBILET BOOKING AKIÞI                         ?
???????????????????????????????????????????????????????????????????
?                                                                 ?
?  ADIM 1: UÇUÞ ARAMA                                           ?
?  POST /api/flight/search                                        ?
?  ??? Backend: Login ? AirSearch (Biletbank SOAP)               ?
?  ??? Çýktý: flights[], sessionId, sessionToken, searchId       ?
?  ??? Session 20dk cache'lenir                                   ?
?                                                                 ?
?  ???? Kullanýcý bir uçuþ seçer ????                            ?
?                                                                 ?
?  ADIM 2: KOLTUK TAHSÝSÝ                                       ?
?  POST /api/flight/allocate                                      ?
?  ??? Girdi: sessionId, sessionToken, productId                 ?
?  ??? Çýktý: shoppingFileId, passengers[], bookingItems[],      ?
?  ?          brandedFareItems[], canBeReserved                   ?
?  ??? ?? DALLANMA NOKTASI:                                      ?
?      ??? canBeReserved == true  ? HoldReservation akýþý        ?
?      ?   (Önce reserve et, sonra ödeme)                        ?
?      ??? canBeReserved == false ? Instant Ticketing akýþý      ?
?          (Ödeme yapýlmadan bilet kesilemez, direkt ödeme)       ?
?                                                                 ?
?  ADIM 3: YOLCU BÝLGÝSÝ GÜNCELLEME                             ?
?  POST /api/flight/update-passengers                             ?
?  ??? Girdi: sessionId, sessionToken, shoppingFileId,           ?
?  ?          productId, productItemId, passengers[], contact     ?
?  ??? ?? TempTag = Allocate'ten gelen PaxReferenceId            ?
?  ??? Çýktý: { hasError, errorMessage }                         ?
?                                                                 ?
?  ADIM 4: ÖN REZERVASYON                                       ?
?  POST /api/flight/make-prebooking                               ?
?  ??? Girdi: sessionId, sessionToken, productId,                ?
?  ?          brandedFareItemId, shoppingFileId,                  ?
?  ?          passengers[], contact, userId?                      ?
?  ??? Backend: Biletbank MakePrebooking + DB Booking kaydý      ?
?  ??? Çýktý: bookingCode (PNR), status, expiresAt, bookingId   ?
?                                                                 ?
?  ???????????????????????????????????????????                    ?
?  AÞAÐIDAKÝ ADIMLAR HENÜZ ÝMPLEMENTE EDÝLMEDÝ                 ?
?  ???????????????????????????????????????????                    ?
?                                                                 ?
?  ADIM 5: ÖDEME ? HENÜZ YOK                                   ?
?  POST /api/flight/make-payment (planlanan)                      ?
?  ??? Girdi: sessionId, sessionToken, shoppingFileId,           ?
?  ?          kart bilgileri, 3D Secure redirect URL              ?
?  ??? Çýktý: ödeme sonucu, 3DS redirect URL                    ?
?                                                                 ?
?  ADIM 6: ALIÞVERÝÞ SONLANDIRMA ? HENÜZ YOK                   ?
?  POST /api/flight/finalize-shopping (planlanan)                 ?
?  ??? Girdi: sessionId, sessionToken, shoppingFileId            ?
?  ??? Çýktý: bilet durumu, e-ticket numaralarý                 ?
?                                                                 ?
?  ADIM 7: DURUM KONTROLÜ ? HENÜZ YOK                           ?
?  POST /api/flight/poke-shopping-file (planlanan)                ?
?  ??? Girdi: sessionId, sessionToken, shoppingFileId            ?
?  ??? Çýktý: güncel dosya durumu                                ?
?                                                                 ?
?  ADIM 8: BÝLET OKUMA ? HENÜZ YOK                              ?
?  POST /api/flight/read-shopping-file (planlanan)                ?
?  ??? Girdi: sessionId, sessionToken, shoppingFileId            ?
?  ??? Çýktý: detaylý bilet bilgisi, e-ticket numaralarý        ?
?                                                                 ?
?  ADIM 9: OTURUM KAPATMA ? HENÜZ YOK                           ?
?  POST /api/flight/logout (planlanan)                            ?
?  ??? Girdi: sessionId, sessionToken                            ?
?  ??? Çýktý: baþarý/hata                                        ?
?                                                                 ?
???????????????????????????????????????????????????????????????????
```

### Hata Durumunda Davranýþlar

| Adým | Hata Durumu | Ne Yapýlmalý |
|------|-------------|--------------|
| Search | Login hatasý | Retry (max 1–2) sonra kullanýcýya hata göster |
| Search | AirSearch boþ sonuç | "Uçuþ bulunamadý" göster |
| Allocate | Session expired | Yeni Search yap |
| Allocate | ProductId geçersiz | Yeni Search yap |
| Allocate | `isPriceChanged: true` | Kullanýcýya fiyat deðiþikliði uyarýsý göster |
| UpdatePassengers | TempTag eþleþmeme | Allocate'i tekrarla, yeni TempTag'ler al |
| MakePreBooking | Biletbank hatasý | Allocate'ten baþla |
| MakePreBooking | DB kayýt hatasý | PNR döner ama DB kaydý olmaz (log'lanýr) |

### canBeReserved Dallanma Detayý

```
canBeReserved == true (HoldReservation)
??? UpdatePassengers ? MakePreBooking ? (bekletme süresi var)
??? prebookingExpiresAt / reservationExpiresAt ile süre takibi
??? Süre içinde MakePayment ? FinalizeShopping
??? Süre dolarsa rezervasyon otomatik iptal

canBeReserved == false (Instant Ticketing)
??? UpdatePassengers ? MakePreBooking
??? Hemen ardýndan MakePayment (zorunlu)
??? Ödeme baþarýlý ? FinalizeShopping ? Bilet kesilir
??? Gecikme riski: fiyat deðiþebilir
```

---

## 6. DATA MODELLERÝ (TypeScript Interface'leri)

Frontend'in doðrudan kullanabileceði TypeScript tip tanýmlarý:

```typescript
// ???????????????????????????????????????????
// SEARCH
// ???????????????????????????????????????????

interface SearchRequest {
  origin: string;                    // Zorunlu — IATA kodu (3 harf)
  destination: string;               // Zorunlu — IATA kodu (3 harf)
  originCountryCode?: string;        // Varsayýlan: "TR"
  destinationCountryCode?: string;   // Varsayýlan: "TR"
  originIsCity?: boolean;            // Varsayýlan: false
  destinationIsCity?: boolean;       // Varsayýlan: false
  departureDate: string;             // Zorunlu — "yyyy-MM-dd"
  returnDate?: string | null;        // RT ise zorunlu — "yyyy-MM-dd"
  flightType?: "OW" | "RT" | "MP";  // Varsayýlan: "OW"
  flightClass?: "Economy" | "Business" | "First" | "Comfort"; // Varsayýlan: "Economy"
  adultCount?: number;               // Varsayýlan: 1 — Max: 9 (child ile toplamda)
  childCount?: number;               // Varsayýlan: 0
  infantCount?: number;              // Varsayýlan: 0 — adultCount'u geçemez
  directFlightsOnly?: boolean;       // Varsayýlan: false
  refundablesOnly?: boolean;         // Varsayýlan: false
  searchTimeoutMilliseconds?: number;
  preferredAirlines?: string[];      // IATA kodlarý: ["TK", "PC"]
  searchReason?: "SearchOnly" | "SearchAndBook"; // Varsayýlan: "SearchAndBook"
}

interface FlightSearchResponse {
  hasError: boolean;
  errorMessage: string | null;
  searchId: string | null;
  sessionId: string | null;          // ?? Allocate'e taþýnacak
  sessionToken: string | null;       // ?? Allocate'e taþýnacak
  flights: FlightResult[];
  filterOptions: FlightFilterOptions | null;
}

interface FlightResult {
  // Kimlik
  productId: string | null;          // ?? Allocate'te kullanýlacak
  productItemId: string | null;
  
  // Havayolu
  airlineCode: string | null;        // "TK"
  airlineName: string | null;        // "Türk Hava Yollarý" (backend map ediyor)
  flightNumber: string | null;       // "TK2134"
  bookingProvider: string | null;    // "Amadeus" (Biletbank'tan)
  
  // Güzergah
  originCode: string | null;
  originName: string | null;         // backend map ediyor
  destinationCode: string | null;
  destinationName: string | null;    // backend map ediyor
  
  // Zaman
  departureDate: string | null;      // "2025-07-15"
  departureTime: string | null;      // "08:30"
  arrivalDate: string | null;
  arrivalTime: string | null;
  durationHours: number;
  durationMinutes: number;
  durationFormatted: string | null;  // "1s 30dk" (backend format ediyor)
  
  // Uçak
  equipment: string | null;          // "738", "32A" (IATA uçak tipi kodu)
  
  // Fiyat
  baseFare: number;                  // Biletbank ? BaseFare
  taxes: number;                     // Biletbank ? Taxes
  serviceFee: number;                // Biletbank ? ServiceFee
  totalFare: number;                 // Biletbank ? TotalFare
  currency: string | null;           // "TRY"
  totalFareFormatted: string | null; // "1.065,00 TL" (backend format ediyor)
  
  // Durum
  isRefundable: boolean;             // Biletbank ? IsRefundable
  isReservable: boolean;             // Biletbank ? IsReservable
  refundableText: string | null;     // backend ? "Ýade edilebilir" / "Ýade edilemez"
  
  // Sýnýf
  fareType: string | null;           // Biletbank ? FareType
  bookingClass: string | null;       // "Y", "V", "L" vb.
  bookingClassName: string | null;   // backend map ? "Ekonomi Esnek"
  
  // Kapasite
  availableSeats: number;
  availableSeatsText: string | null; // backend ? "5 koltuk kaldý"
  
  // Aktarma
  stopCount: number;
  isDirect: boolean;
  stopText: string | null;           // "Direkt" / "1 Aktarma"
  
  // Alt segmentler
  segments: FlightSegmentDto[];
  
  // Komisyon
  customerCommissionMin: number;
  customerCommissionMax: number;
  customerCommissionValue: number;
  
  // Tarife paketleri
  brandedFareItems: BrandedFareItem[];
  freeBaggageAllowances: FreeBaggageAllowance[];
}

interface FlightSegmentDto {
  sequenceNo: number;
  originCode: string | null;
  originName: string | null;
  destinationCode: string | null;
  destinationName: string | null;
  departureDate: string | null;
  departureTime: string | null;
  arrivalDate: string | null;
  arrivalTime: string | null;
  durationHours: number;
  durationMinutes: number;
  durationFormatted: string | null;
  airlineCode: string | null;
  airlineName: string | null;
  flightNumber: string | null;
  equipment: string | null;
  bookingClass: string | null;
  bookingClassName: string | null;
  fareType: string | null;
  fareTypeName: string | null;
  layoverMinutes: number | null;     // Ýlk segment'te null
  layoverFormatted: string | null;
}

interface FlightFilterOptions {
  minPrice: number;
  maxPrice: number;
  airlines: AirlineFilterItem[];
  hasDirectFlights: boolean;
  hasRefundableFlights: boolean;
  earliestDeparture: string | null;  // "06:00"
  latestDeparture: string | null;    // "22:45"
}

interface AirlineFilterItem {
  code: string | null;
  name: string | null;
}

interface BrandedFareItem {
  brandedFareItemId: string | null;
  brandedFarePassengers: BrandedFarePassenger[];
  totalFareInfo: BrandedFareTotalInfo | null;
  brandedItems: BrandedItem[];
}

interface BrandedFarePassenger {
  passengerCount: number;
  passengerType: string | null;      // "ADT", "CHD", "INF"
  fareComponents: FareComponent[];
  passengerFareInfo: PassengerFareInfo | null;
  policy: FarePolicy | null;
}

interface FareComponent {
  brandId: string | null;
  bookingClass: string | null;
  cabinClass: string | null;
  fareBasisCode: string | null;
  freeBaggageAllowanceId: string | null;
  availableSeats: number;
  segmentId: string | null;
}

interface PassengerFareInfo {
  baseFare: number;
  taxes: number;
  totalFare: number;
  currency: string | null;
  paxSequence: number;
  paxType: string | null;
}

interface FarePolicy {
  cancellationPolicies: CancellationPolicy[];
  changePolicies: ChangePolicy[];
}

interface CancellationPolicy {
  amount: number;                    // Ýptal ücreti
  applicability: string | null;      // "BeforeDeparture" | "AfterDeparture"
  minutesToDeparture: number;
  currency: string | null;
  isRefundable: boolean;
}

interface ChangePolicy {
  amount: number;                    // Deðiþiklik ücreti
  applicability: string | null;
  minutesToDeparture: number;
  currency: string | null;
  isChangeable: boolean;
}

interface BrandedFareTotalInfo {
  totalFare: number;
  totalTaxes: number;
}

interface BrandedItem {
  brandCode: string | null;          // "ECO", "FLEX", "PREMIUM"
  brandId: string | null;
  brandName: string | null;          // "EcoFly", "ExtraFly"
  brandedRules: BrandedRule[];
}

interface BrandedRule {
  application: string | null;        // "F" (Free), "C" (Chargeable), "N" (Not Available)
  displayType: string | null;
  ruleDescription: string | null;    // "20 kg bagaj dahil"
  serviceGroup: string | null;       // "BG" (Baggage), "SA" (Seat), "ML" (Miles)
}

interface FreeBaggageAllowance {
  allowance: string | null;          // "20"
  category: string | null;           // "Checked" | "Cabin"
  type: string | null;               // "Weight" | "Piece"
  unit: string | null;               // "K" (Kilogram) | "N" (Piece)
  paxType: string | null;            // "ADT"
}

// ???????????????????????????????????????????
// SORT & FILTER
// ???????????????????????????????????????????

interface FlightSortRequest {
  sortBy: "price" | "cheapest" | "earliest" | "latest" | "shortest" | "duration";
  flights: FlightResult[];
}

interface FlightFilterRequest {
  directOnly?: boolean;
  refundableOnly?: boolean;
  minPrice?: number;
  maxPrice?: number;
  airlineCodes?: string[];           // ["TK", "PC"]
  departureTimeFrom?: string;        // "08:00"
  departureTimeTo?: string;          // "18:00"
  flights: FlightResult[];
}

interface FlightFilterResponse {
  flights: FlightResult[];
  filterOptions: FlightFilterOptions;
}

// ???????????????????????????????????????????
// SESSION
// ???????????????????????????????????????????

interface FlightSessionData {
  searchId: string | null;
  shoppingFileId: string | null;
  sessionId: string | null;
  sessionToken: string | null;
}

// ???????????????????????????????????????????
// ALLOCATE
// ???????????????????????????????????????????

interface AllocateRequest {
  sessionId?: string | null;         // Search'ten (opsiyonel)
  sessionToken?: string | null;      // Search'ten (opsiyonel)
  searchRequest?: SearchRequest | null; // Session yoksa zorunlu
  productId: string;                 // Zorunlu
  selectedServiceFee?: number;       // Varsayýlan: 0
}

interface AllocateResponse {
  hasError: boolean;
  errorMessage: string | null;
  shoppingFileId: string | null;
  lastAllocatedProductIds: string[];
  isPriceChanged: boolean;
  isFlightInfoChanged: boolean | null;
  currency: string | null;
  canBeReserved: boolean;            // ?? Dallanma noktasý
  isCreditCardPaymentEnabled: boolean;
  isRunningAccountPaymentEnabled: boolean;
  maxServiceCommission: number;
  minServiceCommission: number;
  customerInfo: AllocateCustomerInfo | null;
  airBookings: AllocateAirBooking[];
  priceSummary: AllocatePriceSummary | null;
  passengers: AllocatePassenger[];
  sessionId: string | null;
  sessionToken: string | null;
  rawSoapResponse: string | null;    // ?? Debug — production'da kaldýrýlacak
  debugInfo: string | null;          // ?? Debug — production'da kaldýrýlacak
}

interface AllocateAirBooking {
  productId: string | null;
  pnr: string | null;
  providerId: string | null;
  status: string | null;
  currency: string | null;
  totalFare: number;
  baseFare: number;
  taxes: number;
  netFare: number;
  serviceFee: number;
  lastSellerCommission: number;
  isRefundable: boolean;
  canBeReserved: boolean;
  validatingCarrier: string | null;
  flightType: string | null;
  bookingItems: AllocateBookingItem[];
  segments: AllocateSegment[];
  brandedFareItems: AllocateBrandedFareItem[];
  brandedItems: AllocateBrandedItem[];
  baggageAllowances: AllocateBaggageAllowance[];
}

interface AllocateBookingItem {
  productItemId: string | null;      // ?? UpdatePassengers'da kullanýlacak
  currency: string | null;
  baseFare: number;
  taxes: number;
  totalFare: number;
  netFare: number;
  serviceFee: number;
  systemServiceFee: number;
  baggage: string | null;
  paxType: string | null;
  paxSequenceNo: number;
  paxReferenceId: string | null;     // ?? UpdatePassengers'da TempTag olarak kullanýlacak
}

interface AllocateSegment {
  segmentId: string | null;
  originCode: string | null;
  destinationCode: string | null;
  departureDay: string | null;
  departureTime: string | null;
  arrivalDay: string | null;
  arrivalTime: string | null;
  marketingAirline: string | null;
  operatingAirline: string | null;
  flightNumber: string | null;
  bookingClass: string | null;
  fareBasis: string | null;
  duration: string | null;
  selectedBrandedFareItemId: string | null;
  sequenceNo: number;
}

interface AllocateBrandedFareItem {
  brandedFareItemId: string | null;  // ?? MakePreBooking'de kullanýlacak
  currency: string | null;
  totalFare: number;
  totalTaxes: number;
  passengers: AllocateBrandedFarePassenger[];
}

interface AllocateBrandedFarePassenger {
  passengerType: string | null;
  passengerCount: number;
  baseFare: number;
  taxes: number;
  totalFare: number;
  currency: string | null;
  bookingClass: string | null;
  cabinClass: string | null;
  fareBasisCode: string | null;
  brandId: string | null;
  seatsAvailable: number;
}

interface AllocateBrandedItem {
  brandId: string | null;
  brandCode: string | null;
  brandName: string | null;
  rules: AllocateBrandedRule[];
}

interface AllocateBrandedRule {
  application: string | null;
  displayType: string | null;
  ruleDescription: string | null;
  serviceGroup: string | null;
}

interface AllocateBaggageAllowance {
  id: string | null;
  paxType: string | null;
  allowance: string | null;
  category: string | null;
  type: string | null;
  unit: string | null;
}

interface AllocateCustomerInfo {
  businessId: string | null;
  businessName: string | null;
  email: string | null;
  username: string | null;
}

interface AllocatePriceSummary {
  grandTotal: number;
  totalBaseFare: number;
  totalTaxes: number;
  totalServiceFee: number;
  currency: string | null;
  priceItems: AllocatePriceItem[];
}

interface AllocatePriceItem {
  productId: string | null;
  productType: string | null;
  total: number;
}

interface AllocatePassenger {
  tempTag: string | null;            // ?? UpdatePassengers'da gönderilecek
  sequenceNo: number;
  type: string | null;               // "ADT", "CHD", "INF"
  paxReferenceId: string | null;     // ?? UpdatePassengers'da gönderilecek
}

// ???????????????????????????????????????????
// UPDATE PASSENGERS
// ???????????????????????????????????????????

interface UpdatePassengersRequest {
  sessionId: string;                 // Allocate'ten
  sessionToken: string;              // Allocate'ten
  shoppingFileId: string;            // Allocate'ten
  productId: string;                 // Allocate ? airBookings[0].productId
  productItemId: string;             // Allocate ? bookingItems[0].productItemId
  passengers: UpdatePassengerItem[];
  contact: UpdatePassengerContact;
}

interface UpdatePassengerItem {
  paxType: "ADT" | "CHD" | "INF";   // Varsayýlan: "ADT"
  sequenceNo: number;                // 1-indexed
  firstName: string;                 // Zorunlu — büyük harf önerilir
  lastName: string;                  // Zorunlu — büyük harf önerilir
  gender: "M" | "F";                // Zorunlu
  birthDate: string;                 // Zorunlu — "yyyy-MM-dd"
  citizenNo?: string | null;         // TC Kimlik — 11 hane
  passportNo?: string | null;        // Uluslararasý uçuþlarda
  passportCountry?: string | null;   // Pasaport ülke kodu
  nationality?: string;              // Varsayýlan: "TR"
  tempTag?: string | null;           // Allocate'ten — boþsa paxReferenceId kullanýlýr
  paxReferenceId?: string | null;    // Zorunlu — Allocate ? passengers[].paxReferenceId
}

interface UpdatePassengerContact {
  email: string;                     // Zorunlu
  phone: string;                     // Zorunlu — "+905551234567" formatý
}

interface UpdatePassengersResponse {
  hasError: boolean;
  errorMessage: string | null;
}

// ???????????????????????????????????????????
// MAKE PRE-BOOKING
// ???????????????????????????????????????????

interface MakePreBookingRequest {
  sessionId: string;
  sessionToken: string;
  productId: string;                  // Allocate ? airBookings[0].productId
  brandedFareItemId: string;          // Allocate ? airBookings[0].brandedFareItems[0].brandedFareItemId
  shoppingFileId: string;
  userId?: string | null;             // Kayýtlý kullanýcý GUID — null ise misafir
  passengers: UpdatePassengerItem[];  // DB kaydý için
  contact: UpdatePassengerContact;    // DB kaydý ve misafir session için
}

interface MakePreBookingResponse {
  hasError: boolean;
  errorMessage: string | null;
  bookingCode: string | null;         // PNR kodu
  status: string | null;              // "Reserved", "PreBooked", "Confirmed"
  totalFare: number;
  baseFare: number;
  taxes: number;
  serviceFee: number;
  currency: string | null;
  shoppingFileId: string | null;
  isPriceChanged: boolean;
  prebookingExpiresAt: string | null; // ISO 8601 datetime
  reservationExpiresAt: string | null;
  segments: PreBookingSegment[];
  passengers: PreBookingPassenger[];
  bookingId: string | null;           // Backend DB booking ID (GUID)
  userId: string | null;
  guestSessionId: string | null;
  isGuest: boolean;
}

interface PreBookingSegment {
  segmentId: string | null;
  originCode: string | null;
  destinationCode: string | null;
  departureDay: string | null;
  departureTime: string | null;
  arrivalDay: string | null;
  arrivalTime: string | null;
  flightNumber: string | null;
  marketingAirline: string | null;
  bookingClass: string | null;
}

interface PreBookingPassenger {
  firstName: string | null;
  lastName: string | null;
  type: string | null;
  citizenNo: string | null;
  gender: string | null;
}

// ???????????????????????????????????????????
// YARDIMCI
// ???????????????????????????????????????????

interface Airport {
  id: number;
  iataCode: string;
  icaoCode: string | null;
  nameTr: string;
  nameEn: string;
  cityTr: string;
  cityEn: string;
  countryTr: string;
  countryEn: string;
  countryCode: string;
  timezone: string | null;
  isCity: boolean;
  isDomestic: boolean;
  isActive: boolean;
  sortOrder: number;
}

interface Airline {
  id: number;
  code: string;
  nameTr: string;
  nameEn: string;
  logoUrl: string | null;
  isActive: boolean;
}

// ???????????????????????????????????????????
// HATA FORMATI
// ???????????????????????????????????????????

/** Validasyon hatalarý (400) */
interface ValidationError {
  error: string;
}

/** Sunucu hatalarý (500) */
interface ServerError {
  error: string;
  inner?: string;
}

/** Biletbank iþ mantýðý hatalarý (200 + hasError=true) */
interface BusinessError {
  hasError: true;
  errorMessage: string;
}
```

---

## 7. GÜVENLÝK RAPORU

### ?? KRÝTÝK Bulgular

| # | Bulgu | Risk | Öneri |
|---|-------|------|-------|
| 1 | **Authentication/Authorization YOK** | ?? Kritik | Tüm endpoint'ler açýk. JWT Bearer token veya API Key mekanizmasý eklenmeli |
| 2 | **Biletbank credential'larý `appsettings.json`'da** | ?? Kritik | Environment variables, Azure Key Vault veya User Secrets kullanýlmalý |
| 3 | **500 response'larýnda `ex.Message` + `InnerException` dönüyor** | ?? Yüksek | Stack trace ve internal bilgi sýzmasý. Production'da generic error mesajý dönmeli |
| 4 | **`rawSoapResponse` ve `debugInfo` production'da dönüyor** | ?? Yüksek | Biletbank credential/session bilgileri sýzabilir. Debug field'larý kaldýrýlmalý |
| 5 | **CORS sadece localhost'a açýk** | ?? Orta | Production deployment'ta güncellenecek — ama wildcard eklenmemeli |
| 6 | **Input validation manual** | ?? Yüksek | FluentValidation veya Data Annotations ile standarize edilmeli |
| 7 | **Rate limiting YOK** | ?? Yüksek | Biletbank API'sine aþýrý istek gidebilir. `AspNetCoreRateLimit` veya built-in rate limiter eklenmeli |
| 8 | **TC Kimlik / kart bilgisi þifreleme YOK** | ?? Kritik | Transport layer HTTPS var ama at-rest encryption yok |
| 9 | **SQL Injection korumasý** | ?? Düþük | EF Core parametrize query kullanýyor — raw SQL sadece `Program.cs`'de migration için, user input yok |
| 10 | **XSS korumasý** | ?? Düþük | API-only proje, HTML render yok. JSON serializer `UnsafeRelaxedJsonEscaping` kullanýyor — dikkat |
| 11 | **Retry mekanizmasý YOK** | ?? Orta | Biletbank geçici hatalarýnda Polly retry pattern eklenmeli |

### Ödeme (PCI-DSS) Durumu

> **Þu an ödeme endpoint'i YOK.** Ýmplemente edilirken:
> - Kart bilgisi backend'e **GELMEMELÝ** — 3D Secure redirect veya tokenization (iyzico, PayTR, Stripe) kullanýlmalý
> - Kart numarasý loglanmamalý
> - PCI-DSS SAQ-A veya SAQ-A-EP uyumu hedeflenmeli

---

## 8. FRONTEND ÝÇÝN ÖNERÝLER

### 8.1 — Her Request'te Gönderilmesi Gereken Header'lar

```
Content-Type: application/json
```

> Þu an baþka header (Authorization, X-API-Key vb.) **gerekmiyor** çünkü auth mekanizmasý yok. Auth eklendiðinde:
> ```
> Authorization: Bearer <jwt-token>
> ```

### 8.2 — Error Response Parse Mantýðý

Frontend'in 3 tip hata senaryosu yönetmesi gerekir:

```typescript
async function apiCall<T>(url: string, body?: any): Promise<T> {
  const response = await fetch(url, {
    method: body ? 'POST' : 'GET',
    headers: { 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });
  
  const data = await response.json();
  
  // Tip 1: HTTP hata kodu (400, 404, 500)
  if (!response.ok) {
    throw new Error(data.error || `HTTP ${response.status}`);
  }
  
  // Tip 2: Biletbank iþ mantýðý hatasý (200 + hasError=true)
  if (data.hasError) {
    throw new Error(data.errorMessage || 'Bilinmeyen bir hata oluþtu');
  }
  
  // Tip 3: Baþarýlý
  return data as T;
}
```

### 8.3 — Diðer Notlar

| Konu | Durum |
|------|-------|
| Pagination | **YOK** — Uçuþ sonuçlarý tek seferde dönüyor |
| WebSocket / SSE | **YOK** — Gerçek zamanlý durum takibi yok |
| Dosya upload | **YOK** — Pasaport fotoðrafý vb. yok |
| Webhook / Callback | **YOK** — Ödeme sonucu bildirimi için planlama yapýlabilir |
| Localization | **Kýsmen** — Backend Türkçe mesajlar dönüyor, çoklu dil desteði yok |

### 8.4 — Frontend Session Yönetimi Önerisi

```typescript
// Frontend'in yönetmesi gereken state
interface BookingSession {
  // Search'ten gelen
  sessionId: string;
  sessionToken: string;
  searchId: string;
  
  // Allocate'ten gelen
  shoppingFileId: string;
  productId: string;
  productItemId: string;           // bookingItems[0].productItemId
  brandedFareItemId: string;       // brandedFareItems[0].brandedFareItemId
  passengers: AllocatePassenger[]; // tempTag + paxReferenceId
  canBeReserved: boolean;
  
  // MakePreBooking'ten gelen
  bookingCode: string;             // PNR
  bookingId: string;               // DB ID
  prebookingExpiresAt: string;
}
```

---

## 9. EKSÝK ENDPOINT / ÖZELLÝK LÝSTESÝ (BACKLOG)

### ?? Kritik — Booking akýþýný tamamlamak için gerekli

| # | Endpoint / Özellik | Biletbank Servisi | Öncelik |
|---|---|---|---|
| 1 | `POST /api/flight/remove-product` | `RemoveProduct` | P1 |
| 2 | `POST /api/flight/make-payment` | `MakePayment` | P1 |
| 3 | `POST /api/flight/finalize-shopping` | `FinalizeShopping` | P1 |
| 4 | `POST /api/flight/poke-shopping-file` | `PokeShoppingFile` | P1 |
| 5 | `POST /api/flight/read-shopping-file` | `ReadShoppingFile` | P1 |
| 6 | `POST /api/flight/logout` | `Logout` | P2 |

### ?? Yüksek — Güvenlik ve kullanýcý yönetimi

| # | Özellik | Açýklama | Öncelik |
|---|---------|----------|---------|
| 7 | JWT Authentication | Kullanýcý giriþi / token yönetimi | P1 |
| 8 | User Registration / Login | `POST /api/auth/register`, `POST /api/auth/login` | P1 |
| 9 | Rate Limiting | Endpoint bazlý istek sýnýrlamasý | P1 |
| 10 | Input Validation | FluentValidation veya Data Annotations | P1 |
| 11 | Production error handling | Generic error response, stack trace gizleme | P1 |
| 12 | Debug field'larý kaldýrma | `rawSoapResponse`, `debugInfo` ? prod'da null | P2 |

### ?? Orta — Ýyileþtirmeler

| # | Özellik | Açýklama | Öncelik |
|---|---------|----------|---------|
| 13 | Retry mekanizmasý (Polly) | Biletbank geçici hatalarý için | P2 |
| 14 | Booking geçmiþi endpoint'i | `GET /api/booking/history` | P2 |
| 15 | Booking detay endpoint'i | `GET /api/booking/{id}` | P2 |
| 16 | Booking iptal endpoint'i | `POST /api/booking/{id}/cancel` | P2 |
| 17 | Health check endpoint | `/health` — Biletbank baðlantý durumu dahil | P3 |
| 18 | Swagger XML dokümantasyonu | Controller'lara XML comment ekleme | P3 |
| 19 | Response caching | Sýk aranan rotalar için | P3 |
| 20 | Logging structured (Serilog) | JSON formatted log, Seq/ELK entegrasyonu | P3 |

---

## 10. BÝLETBANK ALAN HARÝTASI (Backend Mapping)

### Backend'in Eklediði Alanlar (Biletbank'ta Yok)

| Frontend Alaný | Kaynak |
|---|---|
| `airlineName` | Backend ? `FlightMappings` dictionary (DB'den yükleniyor) |
| `originName`, `destinationName` | Backend ? Airport DB lookup |
| `durationFormatted` | Backend ? `{h}s {m}dk` formatý |
| `totalFareFormatted` | Backend ? `{amount:N2} TL` formatý |
| `refundableText` | Backend ? "Ýade edilebilir" / "Ýade edilemez" |
| `bookingClassName` | Backend ? Booking class mapping |
| `availableSeatsText` | Backend ? "{n} koltuk kaldý" |
| `stopText` | Backend ? "Direkt" / "1 Aktarma" |
| `isDirect` | Backend ? `segments.count == 1` |
| `layoverMinutes/Formatted` | Backend ? segment arasý süre hesaplama |

### Biletbank Orijinal Alan Ýsimleri ? Backend Mapping

| Biletbank (SOAP XML) | Backend C# Property | Frontend JSON |
|---|---|---|
| `T_FlightOption.ProductId` | `FlightOption.ProductId` | `productId` |
| `T_Segment.DepartureDay` | `FlightSegment.DepartureDay` | `departureDate` |
| `T_Segment.DepartureTime` | `FlightSegment.DepartureTime` | `departureTime` (PT format ? HH:mm) |
| `T_Segment.Duration` | `FlightSegment.Duration` | `durationHours` + `durationMinutes` |
| `T_PaxFareItem.PaxCode` | `PassengerFareItem.PaxCode` | `paxCode` |
| `T_Passenger.TempTag` | `AllocatePassenger.TempTag` | `tempTag` |
| `PaxReference.PaxReferenceId` | `AllocateBookingItem.PaxReferenceId` | `paxReferenceId` |
| `T_AirBooking.BookingCode` | `AllocateAirBooking.PNR` | `pnr` |
| `ShoppingFile.Id` | `AllocateResponse.ShoppingFileId` | `shoppingFileId` |
| `FlightRuleAttribute.IsReservable` | `AllocateAirBooking.CanBeReserved` | `canBeReserved` |
| `BrandedFareItem.BrandedFareItemId` | `BrandedFareItem.BrandedFareItemId` | `brandedFareItemId` |
| `BrandedRule.Application` | `BrandedRule.Application` | "F" (Free), "C" (Charge), "N" (No) |

---

> **Bu döküman, projenin mevcut durumunu yansýtmaktadýr. Eksik endpoint'ler implemente edildikçe güncellenmelidir.**
