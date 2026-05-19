using GBILET.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Data;

public static class DataSeeder
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        SeedAirlines(modelBuilder);
        SeedFareTypes(modelBuilder);
        SeedBookingClasses(modelBuilder);
        SeedPopularRoutes(modelBuilder);
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
