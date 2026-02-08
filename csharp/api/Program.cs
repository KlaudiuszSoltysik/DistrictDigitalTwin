using api;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddDbContext<HistoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("HistoryDbConnection")));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TelemetryConsumer>();
    x.AddConsumer<SimulationStatusConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var connectionString = builder.Configuration.GetConnectionString("RabbitMqConnection");

        var uri = new Uri(connectionString ?? throw new InvalidOperationException("Invalid connection string."));

        cfg.Host(uri.Host, (ushort)uri.Port, uri.AbsolutePath, h =>
        {
            var parts = uri.UserInfo.Split(':');
            h.Username(parts[0]);
            h.Password(parts[1]);
        });

        cfg.UseRawJsonSerializer();

        cfg.ReceiveEndpoint("cache-service-queue", e =>
        {
            e.Bind("telemetry.exchange");
            e.ConfigureConsumer<TelemetryConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("status", e => { e.ConfigureConsumer<SimulationStatusConsumer>(ctx); });
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSingleton<HistoryCacheService>();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.MapHub<SimulationHub>("/hubs/simulation");

app.Run();

public partial class Program
{
}