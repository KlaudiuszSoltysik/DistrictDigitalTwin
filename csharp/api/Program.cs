using backend;
using backend.Consumers;
using backend.Hubs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TelemetryConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var connectionString = builder.Configuration.GetConnectionString("RabbitMqConnection");

        var uri = new Uri(connectionString ?? throw new InvalidOperationException(message: "Invalid connection string."));

        cfg.Host(uri.Host, (ushort)uri.Port, uri.AbsolutePath, h =>
        {
            var parts = uri.UserInfo.Split(':');
            h.Username(parts[0]);
            h.Password(parts[1]);
        });

        cfg.ReceiveEndpoint("district.telemetry", e =>
        {
            e.UseRawJsonSerializer();
            e.ConfigureConsumer<TelemetryConsumer>(context);
        });
    });
});

if (builder.Environment.EnvironmentName != "Testing")
{
    builder.Services.AddDbContext<PostgresContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));
}

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