using Climate.Contracts.Audit;
using Climate.Sensors.Domain.Communities;
using Climate.Sensors.Domain.Sensors;
using Microsoft.EntityFrameworkCore;

namespace Climate.Sensors.Infrastructure.Persistence;

public sealed class SensorsDbContext(DbContextOptions<SensorsDbContext> options) : DbContext(options)
{
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<Sensor> Sensors => Set<Sensor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.AddAuditOutbox(); modelBuilder.ApplyConfigurationsFromAssembly(typeof(SensorsDbContext).Assembly); }
}
