using api;
using MassTransit;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TelemetryConsumer>();

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

        cfg.ReceiveEndpoint("cache-service-queue", e =>
        {
            e.Bind("district.telemetry.exchange");
            e.UseRawJsonSerializer();
            e.ConfigureConsumer<TelemetryConsumer>(ctx);
        });
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