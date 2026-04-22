using GBILET.Core.Interfaces;
using GBILET.Core.Service;
using GBILET.Core.Service.Flight;
using GBILET.Core.Service.Ticket;
using GBILET.Core.Helpers;
using GBILET.Infrastructure.Data;
using GBILET.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.ResponseCompression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using QuestPDF.Infrastructure;
using GBILET.Core.Service.Admin;
using GBILET.Infrastructure.Services.Admin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Cryptography;
using GBILET.Core.Service.Support;
using GBILET.Infrastructure.Services.Support;


// PostgreSQL: DateTimeKind.Unspecified olan DateTime değerlerini kabul et
// Npgsql 6+ varsayılan olarak sadece UTC kabul eder; bu switch legacy davranışı etkinleştirir
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// QuestPDF Community License
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddScoped<AirportSeeder>();
builder.Services.AddScoped<AirlineSeeder>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

    builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",      // gticket-front (müşteri)
                "http://localhost:5173",      // gticket-front Vite
                "http://localhost:3001",      // gticket-admin (admin panel dev)
                "https://atabilet.com",       // production müşteri
                "https://www.atabilet.com",
                "https://admin.atabilet.com"  // production admin
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();            // Cookie'ler için ŞART
    });
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 300;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 20;
    });


    // YENİ — Admin login için sıkı policy: 5 deneme / 15dk / IP
    options.AddPolicy("admin-login", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // YENİ — Genel admin API: 100 req/dk per IP
    options.AddPolicy("admin-general", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10
        });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            error = new
            {
                code = "RATE_LIMIT_EXCEEDED",
                message = "Too many requests. Please try again later.",
                retry_after = 60
            }
        }, cancellationToken);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// ============================================================
// JWT Authentication — token cookie'den okunur (httpOnly + Secure)
// ============================================================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        var jwtConfig = jwtSection.Get<JwtOptions>() ?? throw new InvalidOperationException("Jwt config missing");

        // Public key'i yükle
        var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"]
            ?? throw new InvalidOperationException("Jwt:PublicKeyPath not configured");

        if (!File.Exists(publicKeyPath))
            throw new FileNotFoundException($"JWT public key not found at {publicKeyPath}");

        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(publicKeyPath));
        var signingKey = new RsaSecurityKey(rsa);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtConfig.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtConfig.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Token Bearer header yerine cookie'den okusun
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("atabilet_access_token", out var token)
                    && !string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<GTicketDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IAirlineRepository, AirlineRepository>();
builder.Services.AddScoped<IAirportRepository, AirportRepository>();
builder.Services.AddScoped<IPopularRouteRepository, PopularRouteRepository>();
builder.Services.AddScoped<ITicketPdfService, TicketPdfService>();

builder.Services.AddHttpClient<ICurrencyService, CurrencyService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

// ============================================================
// ADMIN PANEL — Auth & Services
// ============================================================
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<IAdminUserManagementService, AdminUserManagementService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddSingleton<ITokenService, TokenService>(); // RSA key'ler bir kez yüklensin
builder.Services.AddSingleton<ITotpService, TotpService>();   // Stateless
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IAdminCustomerService, AdminCustomerService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
builder.Services.AddMemoryCache();

builder.Services
.AddHttpClient<IFlightService, BiletBankFlightService>(client =>
{
    client.BaseAddress = new Uri("https://apitest.biletbank.com");
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<GBILET.Core.Service.Auth.IAuthService, GBILET.Infrastructure.Services.AuthService>();


var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GTicketDbContext>();
    db.Database.EnsureCreated();

    // CityCode kolonu entity'ye sonradan eklendi — mevcut DB'de yoksa oluştur
    db.Database.ExecuteSqlRaw(@"ALTER TABLE IF EXISTS ""Airports"" ADD COLUMN IF NOT EXISTS ""CityCode"" varchar(10)");

    // === Havalimanı Seed ===
    var airportSeeder = scope.ServiceProvider.GetRequiredService<AirportSeeder>();
    await airportSeeder.SeedAsync();

    // === Havayolu Seed ===
    var airlineSeeder = scope.ServiceProvider.GetRequiredService<AirlineSeeder>();
    await airlineSeeder.SeedAsync();

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
        db.Database.ExecuteSqlRaw("""
            ALTER TABLE "Bookings" ALTER COLUMN "UserId" DROP NOT NULL;
            """);
    }
    catch
    {
        // Constraint zaten nullable ise veya tablo yoksa hatayi yut
    }

    // GuestSessions tablosunu olustur (yoksa)
    try
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "GuestSessions" (
                "Id"        uuid        NOT NULL PRIMARY KEY,
                "Email"     text,
                "Phone"     text,
                "IpAddress" text,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS "IX_GuestSessions_Email" ON "GuestSessions" ("Email");
            """);
    }
    catch
    {
        // Tablo zaten varsa yut
    }

    // Bookings tablosuna GuestSessionId kolonu ekle (yoksa)
    try
    {
        db.Database.ExecuteSqlRaw("""
            ALTER TABLE "Bookings"
                ADD COLUMN IF NOT EXISTS "GuestSessionId" uuid NULL
                    REFERENCES "GuestSessions"("Id");
            """);
    }
    catch
    {
        // Kolon zaten varsa yut
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseForwardedHeaders();
app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("fixed");
await app.RunAsync();