using Climate.Contracts.Audit;
using Climate.Alerts.Domain.Alerts;
using Microsoft.EntityFrameworkCore;

namespace Climate.Alerts.Infrastructure.Persistence;

public sealed class AlertsDbContext(DbContextOptions<AlertsDbContext> options) : DbContext(options)
{
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<ClimateAlert> Alerts => Set<ClimateAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.AddAuditOutbox(); modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlertsDbContext).Assembly); }
}
