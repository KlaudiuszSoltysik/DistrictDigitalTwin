using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace shared;

public class TelemetryDbContextFactory : IDesignTimeDbContextFactory<TelemetryDbContext>
{
    public TelemetryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TelemetryDbContext>();

        optionsBuilder.UseNpgsql("Host=localhost;Database=dummy_db;Username=postgres;Password=postgres");

        return new TelemetryDbContext(optionsBuilder.Options);
    }
}