using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Services;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HttpClient - SSH tüneli üzerinden localhost:8000'e yönlendirme
builder.Services
    .AddHttpClient<IFlightService, BiletBankFlightService>(client =>
    {
        client.DefaultRequestHeaders.Host = "apitest.biletbank.com";
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