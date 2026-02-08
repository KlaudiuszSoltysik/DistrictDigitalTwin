using Microsoft.EntityFrameworkCore;

namespace shared;

public class HistoryDbContext(DbContextOptions<HistoryDbContext> options) : DbContext(options)
{
    public DbSet<TelemetryEntity> Telemetry { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TelemetryEntity>()
            .HasKey(t => new { t.Id, t.RunId, t.Timestamp });

        modelBuilder.Entity<TelemetryEntity>()
            .HasIndex(t => t.RunId);
    }
}