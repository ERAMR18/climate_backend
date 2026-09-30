using Climate.Contracts.Audit;
using Climate.Monitoring.Domain.Readings;
using Microsoft.EntityFrameworkCore;

namespace Climate.Monitoring.Infrastructure.Persistence;

public sealed class MonitoringDbContext(DbContextOptions<MonitoringDbContext> options) : DbContext(options)
{
    public DbSet<SimulationOverride> SimulationOverrides => Set<SimulationOverride>();
    public DbSet<SensorReading> Readings => Set<SensorReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.AddAuditOutbox(); modelBuilder.ApplyConfigurationsFromAssembly(typeof(MonitoringDbContext).Assembly); }
}
