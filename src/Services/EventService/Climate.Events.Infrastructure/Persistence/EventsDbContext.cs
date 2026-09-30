using Climate.Contracts.Audit;
using Climate.Events.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Climate.Events.Infrastructure.Persistence;

public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options) : DbContext(options)
{
    public DbSet<ClimateEvent> Events => Set<ClimateEvent>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.AddAuditOutbox(); modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly); }
}
