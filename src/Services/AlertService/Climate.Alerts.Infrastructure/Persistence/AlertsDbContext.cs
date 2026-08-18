using Climate.Alerts.Domain.Alerts;
using Microsoft.EntityFrameworkCore;

namespace Climate.Alerts.Infrastructure.Persistence;

public sealed class AlertsDbContext(DbContextOptions<AlertsDbContext> options) : DbContext(options)
{
    public DbSet<ClimateAlert> Alerts => Set<ClimateAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlertsDbContext).Assembly);
}
