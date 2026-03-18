using GBILET.Core.Service;
using GBILET.Core.Service.Flight;
using GBILET.Core.Helpers;
using GBILET.Infrastructure.Data;
using GBILET.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

// PostgreSQL: DateTimeKind.Unspecified olan DateTime değerlerini kabul et
// Npgsql 6+ varsayılan olarak sadece UTC kabul eder; bu switch legacy davranışı etkinleştirir
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173"
              )
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<GTicketDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IAirlineRepository, AirlineRepository>();
builder.Services.AddScoped<IAirportRepository, AirportRepository>();
builder.Services.AddScoped<IPopularRouteRepository, PopularRouteRepository>();

builder.Services.AddMemoryCache();

builder.Services
    .AddHttpClient<IFlightService, BiletBankFlightService>(client =>
    {
        client.BaseAddress = new Uri("https://apitest.biletbank.com");
    });


var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GTicketDbContext>();
    db.Database.EnsureCreated();

    // Bozuk Türkçe karakterleri düzelt (önceki encoding hatalı seed'den kalma)
    try
    {
        var corruptedAirlines = db.Airlines
            .Where(a => a.NameTr.Contains("\uFFFD") || a.NameTr.Contains("T�rk"))
            .ToList();

        if (corruptedAirlines.Count > 0)
        {
            var fixMap = new Dictionary<string, string>
            {
                { "TK", "Türk Hava Yolları" },
                { "PC", "Pegasus Hava Yolları" },
                { "VF", "AnadoluJet" },
                { "XQ", "SunExpress" },
                { "XC", "Corendon Airlines" },
                { "LH", "Lufthansa" },
                { "BA", "British Airways" },
                { "AF", "Air France" },
                { "EK", "Emirates" },
                { "QR", "Qatar Airways" }
            };

            foreach (var airline in corruptedAirlines)
            {
                if (fixMap.TryGetValue(airline.Code, out var fixedName))
                    airline.NameTr = fixedName;
            }

            db.SaveChanges();
        }
    }
    catch
    {
        // Tablo henüz yoksa veya başka hata olursa yut
    }

    // Havayolu verilerini veritabanından yükle
    try
    {
        var airlineRepo = scope.ServiceProvider.GetRequiredService<IAirlineRepository>();
        var airlines = await airlineRepo.GetAirlineDictionaryAsync();
        FlightMappings.LoadAirlinesFromDatabase(airlines);
    }
    catch
    {
        // Veritabanından yüklenemezse hard-coded fallback kullanılır
    }

    // Mevcut DB'de UserId NOT NULL constraint'ini nullable yap

    try
    {
        var dbProviderName = builder.Configuration.GetValue<string>("DbProvider");
        if (dbProviderName == "PostgreSQL")
        {
            db.Database.ExecuteSqlRaw("""
                ALTER TABLE "Bookings" ALTER COLUMN "UserId" DROP NOT NULL;
                """);
        }
        else
        {
            db.Database.ExecuteSqlRaw("""
                ALTER TABLE Bookings ALTER COLUMN UserId uniqueidentifier NULL;
                """);
        }
    }
    catch
    {
        // Constraint zaten nullable ise veya tablo yoksa hatayi yut
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("FrontendPolicy");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();