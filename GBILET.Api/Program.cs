using GBILET.Core.Interfaces;
using GBILET.Core.Service;
using GBILET.Core.Service.Flight;
using GBILET.Core.Service.Sms;
using GBILET.Core.Service.Ticket;
using GBILET.Core.Helpers;
using GBILET.Infrastructure.Data;
using GBILET.Infrastructure.Extensions;
using GBILET.Infrastructure.Services;
using GBILET.Infrastructure.Services.Sms;
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
using GBILET.Core.Service.Email;
using GBILET.Core.Service.Support;
using GBILET.Infrastructure.Services.Email;
using GBILET.Infrastructure.Services.Support;
using GBILET.Core.Models.Flight;
using GBILET.Infrastructure.Caching;
using GBILET.Infrastructure.Resilience;
using GBILET.Infrastructure.Services.FlightChangeRules;
using Polly;
using Polly.Extensions.Http;
using Prometheus;


// PostgreSQL: DateTimeKind.Unspecified olan DateTime değerlerini kabul et
// Npgsql 6+ varsayılan olarak sadece UTC kabul eder; bu switch legacy davranışı etkinleştirir
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// QuestPDF Community License
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseSentry(o =>
{
    o.Dsn = builder.Configuration["Sentry:Dsn"] ?? "";
    o.Debug = false;
    o.TracesSampleRate = 1.0;
    o.Environment = builder.Environment.EnvironmentName;
});

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
                "http://localhost:3000",              // gticket-front (müşteri)
                "http://localhost:5173",              // gticket-front Vite
                "http://localhost:3001",              // gticket-admin (admin panel dev)
                "http://localhost:3010",              // devtest-front local
                "http://localhost:3011",              // devtest-admin local
                "https://atabilet.com",               // production müşteri
                "https://www.atabilet.com",
                "https://anbadmin.atabilet.com",      // production admin (obscure subdomain)
                "https://devtest.atabilet.com",        // devtest müşteri
                "https://devtest-admin.atabilet.com"   // devtest admin
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

    // YENİ — Misafir destek lookup: brute-force koruması (PNR + Soyad)
    // 5 deneme / dk + 30 deneme / saat per IP
    options.AddPolicy("guest-support-lookup", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // Payment endpoint'leri — kart deneme/brute-force koruması
    // 5 deneme / dk per IP. BFF tarafında da 3 req/dk rate-limit var; bu ikinci savunma hattı.
    options.AddPolicy("payment", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
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
builder.Services.AddScoped<IBlogService, BlogService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
builder.Services.AddScoped<IBookingSyncService, BookingSyncService>();
builder.Services.AddSingleton<IGuestSupportTokenService, GuestSupportTokenService>();
builder.Services.AddDataProtection(); // GuestSupportTokenService bunu kullanır
builder.Services.AddMemoryCache();

// ============================================================
// FLIGHT SEARCH MEMORY CACHE + CHANGE DETECTOR + RESILIENCE
// ============================================================
builder.Services.Configure<FlightCacheOptions>(builder.Configuration.GetSection(FlightCacheOptions.SectionName));
builder.Services.AddSingleton<IFlightSearchCache, MemoryFlightSearchCache>();

// Strategy pattern: change rules registered in order, detector iterates by Order property
builder.Services.AddScoped<IFlightChangeRule, FlightNotFoundRule>();
builder.Services.AddScoped<IFlightChangeRule, SoldOutRule>();
builder.Services.AddScoped<IFlightChangeRule, InsufficientSeatsRule>();
builder.Services.AddScoped<IFlightChangeRule, DepartureDayChangedRule>();
builder.Services.AddScoped<IFlightChangeRule, DepartureTimeChangedRule>();
builder.Services.AddScoped<IFlightChangeRule, PriceIncreasedRule>();
builder.Services.AddScoped<IFlightChangeRule, PriceDecreasedRule>();
builder.Services.AddScoped<IFlightChangeRule, NoChangeRule>();
builder.Services.AddScoped<FlightChangeDetector>();
builder.Services.AddScoped<FlightAllocateService>();
builder.Services.AddScoped<SessionRecoveryExecutor>();

builder.Services
.AddHttpClient<IFlightService, BiletBankFlightService>(client =>
{
    client.BaseAddress = new Uri("https://apitest.biletbank.com");
    client.Timeout = TimeSpan.FromSeconds(120);
})
.AddPolicyHandler((sp, _) =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("BiletBankRetry");
    return BiletBankRetryPolicy.Build(logger);
});

builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<GBILET.Core.Service.Auth.IAuthService, GBILET.Infrastructure.Services.AuthService>();

// ============================================================
// SMS & OTP
// ============================================================
builder.Services.AddHttpClient<ISmsService, NetGsmSmsService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<IOtpService, OtpService>();

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

    // Email log tablolari (yoksa olustur) — EnsureCreated mevcut DB'de yeni tablo eklemiyor
    try
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "SentEmails" (
                "Id"                 uuid         NOT NULL PRIMARY KEY,
                "EmailType"          varchar(64)  NOT NULL,
                "IdempotencyKey"     varchar(128) NOT NULL,
                "ToAddress"          varchar(256) NOT NULL,
                "Subject"            varchar(256) NOT NULL,
                "ProviderMessageId"  varchar(128) NULL,
                "BookingId"          uuid         NULL,
                "InternalPnr"        varchar(32)  NULL,
                "SentAt"             timestamp with time zone NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS "IX_SentEmails_IdempotencyKey" ON "SentEmails" ("IdempotencyKey");
            CREATE INDEX IF NOT EXISTS "IX_SentEmails_SentAt"          ON "SentEmails" ("SentAt");
            CREATE INDEX IF NOT EXISTS "IX_SentEmails_InternalPnr"     ON "SentEmails" ("InternalPnr");

            CREATE TABLE IF NOT EXISTS "FailedEmails" (
                "Id"                 uuid         NOT NULL PRIMARY KEY,
                "EmailType"          varchar(64)  NOT NULL,
                "IdempotencyKey"     varchar(128) NOT NULL,
                "ToAddress"          varchar(256) NOT NULL,
                "Subject"            varchar(256) NOT NULL,
                "ErrorMessage"       varchar(500) NOT NULL,
                "AttemptCount"       integer      NOT NULL DEFAULT 1,
                "BookingId"          uuid         NULL,
                "InternalPnr"        varchar(32)  NULL,
                "FailedAt"           timestamp with time zone NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS "IX_FailedEmails_IdempotencyKey" ON "FailedEmails" ("IdempotencyKey");
            CREATE INDEX IF NOT EXISTS "IX_FailedEmails_FailedAt"       ON "FailedEmails" ("FailedAt");
            """);
    }
    catch
    {
        // Tablolar zaten varsa yut
    }
    }
    catch
    {
        // Kolon zaten varsa yut
    }

    // SupportTickets tablosunu oluştur (EnsureCreated mevcut DB'de yeni tablo eklemiyor)
    try
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "SupportTickets" (
                "Id"                uuid                        NOT NULL,
                "TicketNumber"      character varying(32)       NOT NULL,
                "UserId"            uuid                        NULL,
                "GuestSessionId"    uuid                        NULL,
                "BookingId"         uuid                        NULL,
                "Type"              integer                     NOT NULL,
                "Subject"           character varying(200)      NOT NULL,
                "GuestEmail"        text                        NULL,
                "Status"            integer                     NOT NULL,
                "ClosedAt"          timestamp without time zone NULL,
                "ClosedByAdminId"   uuid                        NULL,
                "LastActivityAt"    timestamp without time zone NOT NULL,
                "CreatedAt"         timestamp without time zone NOT NULL,
                "UpdatedAt"         timestamp without time zone NOT NULL,
                CONSTRAINT "PK_SupportTickets" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SupportTickets_TicketNumber" ON "SupportTickets" ("TicketNumber");
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_UserId"           ON "SupportTickets" ("UserId");
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_GuestSessionId"   ON "SupportTickets" ("GuestSessionId");
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_Status"           ON "SupportTickets" ("Status");
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_Type"             ON "SupportTickets" ("Type");
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_LastActivityAt"   ON "SupportTickets" ("LastActivityAt");
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_CreatedAt"        ON "SupportTickets" ("CreatedAt");

            CREATE TABLE IF NOT EXISTS "SupportTicketMessages" (
                "Id"                uuid                        NOT NULL,
                "TicketId"          uuid                        NOT NULL,
                "SenderType"        integer                     NOT NULL,
                "SenderId"          uuid                        NULL,
                "SenderDisplayName" character varying(128)      NOT NULL,
                "Body"              text                        NOT NULL,
                "CreatedAt"         timestamp without time zone NOT NULL,
                CONSTRAINT "PK_SupportTicketMessages" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SupportTicketMessages_SupportTickets_TicketId"
                    FOREIGN KEY ("TicketId") REFERENCES "SupportTickets" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_SupportTicketMessages_TicketId"  ON "SupportTicketMessages" ("TicketId");
            CREATE INDEX IF NOT EXISTS "IX_SupportTicketMessages_CreatedAt" ON "SupportTicketMessages" ("CreatedAt");
            """);
    }
    catch
    {
        // Tablolar zaten varsa yut
    }

    // SupportTickets tablosuna eksik kolonları ekle (eski migration uygulanmamış olabilir)
    try
    {
        db.Database.ExecuteSqlRaw(@"ALTER TABLE IF EXISTS ""SupportTickets"" ADD COLUMN IF NOT EXISTS ""GuestSessionId"" uuid NULL");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE IF EXISTS ""SupportTickets"" ADD COLUMN IF NOT EXISTS ""GuestEmail"" text NULL");
    }
    catch
    {
        // Kolonlar zaten varsa yut
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<GBILET.Api.Middleware.ExceptionHandlingMiddleware>();
app.UseResponseCompression();
app.UseStaticFiles();

// Upload klasörünü uygulama başlarken oluştur
var uploadDir = Path.Combine(
    app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
    "uploads", "blog");
Directory.CreateDirectory(uploadDir);
app.UseForwardedHeaders();
app.UseHttpMetrics();
app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("fixed");
app.MapMetrics("/metrics");
await app.RunAsync();