using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Data; 
using GBILET.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

builder.Services
    .AddHttpClient<IFlightService, BiletBankFlightService>(client =>
    {
        client.BaseAddress = new Uri("https://apitest.biletbank.com");
    });

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();