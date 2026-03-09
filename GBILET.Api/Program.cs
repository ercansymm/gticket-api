using GBILET.Core.Service.Flight;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddHttpClient<IFlightService, BiletBankFlightService>(client =>
    {
        client.BaseAddress = new Uri("https://apitest.biletbank.com");
    });

var app = builder.Build();

// Swagger her ortamda açýk (test aþamasý)
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();