using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using shared;

namespace history_service;

public class DbMigrationWorker(IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HistoryDbContext>();

        await db.Database.EnsureCreatedAsync(stoppingToken);

        try
        {
            await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;", stoppingToken);

            await db.Database.ExecuteSqlRawAsync(
                "SELECT create_hypertable('telemetry', 'Timestamp', if_not_exists => TRUE, migrate_data => TRUE);",
                stoppingToken);
        }
        catch (Exception ex)
        {
            // ignored
        }
    }
}