using GBILET.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Data;

public static class DataSeeder
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        SeedAirports(modelBuilder);
        SeedAirlines(modelBuilder);
        SeedFareTypes(modelBuilder);
        SeedBookingClasses(modelBuilder);
        SeedPopularRoutes(modelBuilder);
    }

    private static void SeedAirports(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Airport>().HasData(
            // Türkiye havalimanları
            new Airport { Id = 1, IataCode = "IST", IcaoCode = "LTFM", NameTr = "İstanbul Havalimanı", NameEn = "Istanbul Airport", CityTr = "İstanbul", CityEn = "Istanbul", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 1 },
            new Airport { Id = 2, IataCode = "SAW", IcaoCode = "LTFJ", NameTr = "Sabiha Gökçen Havalimanı", NameEn = "Sabiha Gokcen Airport", CityTr = "İstanbul", CityEn = "Istanbul", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 2 },
            new Airport { Id = 3, IataCode = "ESB", IcaoCode = "LTAC", NameTr = "Esenboğa Havalimanı", NameEn = "Esenboga Airport", CityTr = "Ankara", CityEn = "Ankara", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 3 },
            new Airport { Id = 4, IataCode = "ADB", IcaoCode = "LTBJ", NameTr = "Adnan Menderes Havalimanı", NameEn = "Adnan Menderes Airport", CityTr = "İzmir", CityEn = "Izmir", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 4 },
            new Airport { Id = 5, IataCode = "AYT", IcaoCode = "LTAI", NameTr = "Antalya Havalimanı", NameEn = "Antalya Airport", CityTr = "Antalya", CityEn = "Antalya", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 5 },
            new Airport { Id = 6, IataCode = "TZX", IcaoCode = "LTCG", NameTr = "Trabzon Havalimanı", NameEn = "Trabzon Airport", CityTr = "Trabzon", CityEn = "Trabzon", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 6 },
            new Airport { Id = 7, IataCode = "BJV", IcaoCode = "LTFE", NameTr = "Milas-Bodrum Havalimanı", NameEn = "Milas-Bodrum Airport", CityTr = "Muğla", CityEn = "Mugla", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 7 },
            new Airport { Id = 8, IataCode = "DLM", IcaoCode = "LTBS", NameTr = "Dalaman Havalimanı", NameEn = "Dalaman Airport", CityTr = "Muğla", CityEn = "Mugla", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 8 },
            new Airport { Id = 9, IataCode = "GZT", IcaoCode = "LTAJ", NameTr = "Gaziantep Havalimanı", NameEn = "Gaziantep Airport", CityTr = "Gaziantep", CityEn = "Gaziantep", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 9 },
            new Airport { Id = 10, IataCode = "ASR", IcaoCode = "LTAP", NameTr = "Kayseri Havalimanı", NameEn = "Kayseri Airport", CityTr = "Kayseri", CityEn = "Kayseri", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 10 },
            new Airport { Id = 11, IataCode = "VAN", IcaoCode = "LTCI", NameTr = "Van Ferit Melen Havalimanı", NameEn = "Van Ferit Melen Airport", CityTr = "Van", CityEn = "Van", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 11 },
            new Airport { Id = 12, IataCode = "DIY", IcaoCode = "LTCC", NameTr = "Diyarbakır Havalimanı", NameEn = "Diyarbakir Airport", CityTr = "Diyarbakır", CityEn = "Diyarbakir", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 12 },
            new Airport { Id = 13, IataCode = "SZF", IcaoCode = "LTFH", NameTr = "Samsun-Çarşamba Havalimanı", NameEn = "Samsun-Carsamba Airport", CityTr = "Samsun", CityEn = "Samsun", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 13 },
            new Airport { Id = 14, IataCode = "ERZ", IcaoCode = "LTCE", NameTr = "Erzurum Havalimanı", NameEn = "Erzurum Airport", CityTr = "Erzurum", CityEn = "Erzurum", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 14 },
            new Airport { Id = 15, IataCode = "KYA", IcaoCode = "LTAN", NameTr = "Konya Havalimanı", NameEn = "Konya Airport", CityTr = "Konya", CityEn = "Konya", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 15 },
            new Airport { Id = 16, IataCode = "MLX", IcaoCode = "LTAT", NameTr = "Malatya Havalimanı", NameEn = "Malatya Airport", CityTr = "Malatya", CityEn = "Malatya", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 16 },
            new Airport { Id = 17, IataCode = "EZS", IcaoCode = "LTCA", NameTr = "Elazığ Havalimanı", NameEn = "Elazig Airport", CityTr = "Elazığ", CityEn = "Elazig", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 17 },
            new Airport { Id = 18, IataCode = "OGU", IcaoCode = "LTCN", NameTr = "Ordu-Giresun Havalimanı", NameEn = "Ordu-Giresun Airport", CityTr = "Ordu", CityEn = "Ordu", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 18 },
            new Airport { Id = 19, IataCode = "HTY", IcaoCode = "LTDA", NameTr = "Hatay Havalimanı", NameEn = "Hatay Airport", CityTr = "Hatay", CityEn = "Hatay", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 19 },
            new Airport { Id = 20, IataCode = "NAV", IcaoCode = "LTAZ", NameTr = "Nevşehir Kapadokya Havalimanı", NameEn = "Nevsehir Cappadocia Airport", CityTr = "Nevşehir", CityEn = "Nevsehir", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 20 },
            new Airport { Id = 21, IataCode = "AOE", IcaoCode = "LTBY", NameTr = "Eskişehir Anadolu Havalimanı", NameEn = "Eskisehir Anadolu Airport", CityTr = "Eskişehir", CityEn = "Eskisehir", CountryTr = "Türkiye", CountryEn = "Turkey", CountryCode = "TR", Timezone = "Europe/Istanbul", IsDomestic = true, SortOrder = 21 },

            // Yurt dışı popüler havalimanları
            new Airport { Id = 22, IataCode = "LHR", IcaoCode = "EGLL", NameTr = "Londra Heathrow Havalimanı", NameEn = "London Heathrow Airport", CityTr = "Londra", CityEn = "London", CountryTr = "İngiltere", CountryEn = "United Kingdom", CountryCode = "GB", Timezone = "Europe/London", IsDomestic = false, SortOrder = 100 },
            new Airport { Id = 23, IataCode = "CDG", IcaoCode = "LFPG", NameTr = "Paris Charles de Gaulle Havalimanı", NameEn = "Paris Charles de Gaulle Airport", CityTr = "Paris", CityEn = "Paris", CountryTr = "Fransa", CountryEn = "France", CountryCode = "FR", Timezone = "Europe/Paris", IsDomestic = false, SortOrder = 101 },
            new Airport { Id = 24, IataCode = "FRA", IcaoCode = "EDDF", NameTr = "Frankfurt Havalimanı", NameEn = "Frankfurt Airport", CityTr = "Frankfurt", CityEn = "Frankfurt", CountryTr = "Almanya", CountryEn = "Germany", CountryCode = "DE", Timezone = "Europe/Berlin", IsDomestic = false, SortOrder = 102 },
            new Airport { Id = 25, IataCode = "AMS", IcaoCode = "EHAM", NameTr = "Amsterdam Schiphol Havalimanı", NameEn = "Amsterdam Schiphol Airport", CityTr = "Amsterdam", CityEn = "Amsterdam", CountryTr = "Hollanda", CountryEn = "Netherlands", CountryCode = "NL", Timezone = "Europe/Amsterdam", IsDomestic = false, SortOrder = 103 },
            new Airport { Id = 26, IataCode = "DXB", IcaoCode = "OMDB", NameTr = "Dubai Uluslararası Havalimanı", NameEn = "Dubai International Airport", CityTr = "Dubai", CityEn = "Dubai", CountryTr = "Birleşik Arap Emirlikleri", CountryEn = "United Arab Emirates", CountryCode = "AE", Timezone = "Asia/Dubai", IsDomestic = false, SortOrder = 104 },
            new Airport { Id = 27, IataCode = "JFK", IcaoCode = "KJFK", NameTr = "New York JFK Havalimanı", NameEn = "John F. Kennedy International Airport", CityTr = "New York", CityEn = "New York", CountryTr = "Amerika Birleşik Devletleri", CountryEn = "United States", CountryCode = "US", Timezone = "America/New_York", IsDomestic = false, SortOrder = 105 }
        );
    }

    private static void SeedAirlines(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Airline>().HasData(
            new Airline { Id = 1, Code = "TK", NameTr = "Türk Hava Yolları", NameEn = "Turkish Airlines", LogoUrl = "/assets/airlines/tk.png", CountryCode = "TR", Alliance = "Star Alliance", IsDomestic = true, SortOrder = 1 },
            new Airline { Id = 2, Code = "PC", NameTr = "Pegasus Hava Yolları", NameEn = "Pegasus Airlines", LogoUrl = "/assets/airlines/pc.png", CountryCode = "TR", IsDomestic = true, SortOrder = 2 },
            new Airline { Id = 3, Code = "VF", NameTr = "AnadoluJet", NameEn = "AnadoluJet", LogoUrl = "/assets/airlines/vf.png", CountryCode = "TR", IsDomestic = true, SortOrder = 3 },
            new Airline { Id = 4, Code = "XQ", NameTr = "SunExpress", NameEn = "SunExpress", LogoUrl = "/assets/airlines/xq.png", CountryCode = "TR", IsDomestic = true, SortOrder = 4 },
            new Airline { Id = 5, Code = "XC", NameTr = "Corendon Airlines", NameEn = "Corendon Airlines", LogoUrl = "/assets/airlines/xc.png", CountryCode = "TR", IsDomestic = true, SortOrder = 5 },
            new Airline { Id = 6, Code = "LH", NameTr = "Lufthansa", NameEn = "Lufthansa", LogoUrl = "/assets/airlines/lh.png", CountryCode = "DE", Alliance = "Star Alliance", IsDomestic = false, SortOrder = 10 },
            new Airline { Id = 7, Code = "BA", NameTr = "British Airways", NameEn = "British Airways", LogoUrl = "/assets/airlines/ba.png", CountryCode = "GB", Alliance = "Oneworld", IsDomestic = false, SortOrder = 11 },
            new Airline { Id = 8, Code = "AF", NameTr = "Air France", NameEn = "Air France", LogoUrl = "/assets/airlines/af.png", CountryCode = "FR", Alliance = "SkyTeam", IsDomestic = false, SortOrder = 12 },
            new Airline { Id = 9, Code = "EK", NameTr = "Emirates", NameEn = "Emirates", LogoUrl = "/assets/airlines/ek.png", CountryCode = "AE", IsDomestic = false, SortOrder = 13 },
            new Airline { Id = 10, Code = "QR", NameTr = "Qatar Airways", NameEn = "Qatar Airways", LogoUrl = "/assets/airlines/qr.png", CountryCode = "QA", Alliance = "Oneworld", IsDomestic = false, SortOrder = 14 }
        );
    }

    private static void SeedFareTypes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FareType>().HasData(
            new FareType { Id = 1, Code = "ECO", NameTr = "Ekonomi", NameEn = "Economy", SortOrder = 1 },
            new FareType { Id = 2, Code = "PEF", NameTr = "Premium Ekonomi", NameEn = "Premium Economy", SortOrder = 2 },
            new FareType { Id = 3, Code = "BUS", NameTr = "Business", NameEn = "Business", SortOrder = 3 },
            new FareType { Id = 4, Code = "FIR", NameTr = "First Class", NameEn = "First Class", SortOrder = 4 }
        );
    }

    private static void SeedBookingClasses(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookingClass>().HasData(
            new BookingClass { Id = 1, Code = "Y", FareTypeId = 1, NameTr = "Ekonomi Full", NameEn = "Economy Full", IsRefundable = true, IsChangeable = true, SortOrder = 1 },
            new BookingClass { Id = 2, Code = "M", FareTypeId = 1, NameTr = "Ekonomi Esnek", NameEn = "Economy Flexible", IsRefundable = true, IsChangeable = true, SortOrder = 2 },
            new BookingClass { Id = 3, Code = "S", FareTypeId = 1, NameTr = "Ekonomi Kısıtlı", NameEn = "Economy Restricted", IsRefundable = false, IsChangeable = true, SortOrder = 3 },
            new BookingClass { Id = 4, Code = "V", FareTypeId = 1, NameTr = "Ekonomi İndirimli", NameEn = "Economy Discounted", IsRefundable = false, IsChangeable = false, SortOrder = 4 },
            new BookingClass { Id = 5, Code = "C", FareTypeId = 3, NameTr = "Business Full", NameEn = "Business Full", IsRefundable = true, IsChangeable = true, SortOrder = 5 },
            new BookingClass { Id = 6, Code = "J", FareTypeId = 3, NameTr = "Business İndirimli", NameEn = "Business Discounted", IsRefundable = true, IsChangeable = true, SortOrder = 6 }
        );
    }

    private static void SeedPopularRoutes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PopularRoute>().HasData(
            new PopularRoute { Id = 1, OriginCode = "IST", DestinationCode = "AYT", DisplayPrice = 899.00m, Currency = "TRY", SortOrder = 1 },
            new PopularRoute { Id = 2, OriginCode = "IST", DestinationCode = "ADB", DisplayPrice = 749.00m, Currency = "TRY", SortOrder = 2 },
            new PopularRoute { Id = 3, OriginCode = "IST", DestinationCode = "TZX", DisplayPrice = 699.00m, Currency = "TRY", SortOrder = 3 },
            new PopularRoute { Id = 4, OriginCode = "ESB", DestinationCode = "IST", DisplayPrice = 599.00m, Currency = "TRY", SortOrder = 4 },
            new PopularRoute { Id = 5, OriginCode = "IST", DestinationCode = "BJV", DisplayPrice = 949.00m, Currency = "TRY", SortOrder = 5 },
            new PopularRoute { Id = 6, OriginCode = "ESB", DestinationCode = "AYT", DisplayPrice = 799.00m, Currency = "TRY", SortOrder = 6 }
        );
    }
}
