using Microsoft.EntityFrameworkCore;

namespace shared;

public class TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : DbContext(options)
{
    public DbSet<SimulationTelemetryEntity> SimulationTelemetry { get; set; }
    public DbSet<DigitalTwinTelemetryEntity> DigitalTwinTelemetry { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SimulationTelemetryEntity>().ToTable("SimulationTelemetry");
        modelBuilder.Entity<DigitalTwinTelemetryEntity>().ToTable("DigitalTwinTelemetry");

        modelBuilder.Entity<SimulationTelemetryEntity>()
            .HasKey(t => new { t.Id, t.RunId, t.Timestamp });
        modelBuilder.Entity<SimulationTelemetryEntity>()
            .HasIndex(t => t.RunId);

        modelBuilder.Entity<DigitalTwinTelemetryEntity>()
            .HasKey(t => new { t.RunId, t.Timestamp });
        modelBuilder.Entity<DigitalTwinTelemetryEntity>()
            .HasIndex(t => t.RunId);
    }
}