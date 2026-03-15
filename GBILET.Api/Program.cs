using GBILET.Core.Service;
using GBILET.Core.Service.Flight;
using GBILET.Core.Helpers;
using GBILET.Infrastructure.Data;
using GBILET.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

var dbProvider = builder.Configuration.GetValue<string>("DbProvider");
if (dbProvider == "PostgreSQL")
{
    builder.Services.AddDbContext<GTicketDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
    );
}
else
{
    builder.Services.AddDbContext<GTicketDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
    );
}

builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IAirlineRepository, AirlineRepository>();

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

    // Havayolu verilerini veritabanýndan yükle
    try
    {
        var airlineRepo = scope.ServiceProvider.GetRequiredService<IAirlineRepository>();
        var airlines = await airlineRepo.GetAirlineDictionaryAsync();
        FlightMappings.LoadAirlinesFromDatabase(airlines);
    }
    catch
    {
        // Veritabanýndan yüklenemezse hard-coded fallback kullanýlýr
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