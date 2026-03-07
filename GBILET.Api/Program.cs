using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Services;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HttpClient - SOCKS5 proxy ile SSH sunucusu üzerinden
builder.Services
    .AddHttpClient<IFlightService, BiletBankFlightService>()
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new SocketsHttpHandler
        {
            Proxy = new WebProxy("socks5://localhost:8000"),
            UseProxy = true,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
            }
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();