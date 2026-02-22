using Microsoft.EntityFrameworkCore;
using shared;

namespace telemetry_service;

public class DbMigrationWorker(IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        await db.Database.MigrateAsync(stoppingToken);

        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;", stoppingToken);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT create_hypertable('\"SimulationTelemetry\"', 'Timestamp', if_not_exists => TRUE, migrate_data => TRUE);"
            , stoppingToken);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT create_hypertable('\"DigitalTwinTelemetry\"', 'Timestamp', if_not_exists => TRUE, migrate_data => TRUE);"
            , stoppingToken);
    }
}