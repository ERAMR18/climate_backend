using Climate.Audit.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
namespace Climate.Audit.Infrastructure.Persistence;
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options):DbContext(options)
{
    public DbSet<AuditLog> AuditLogs=>Set<AuditLog>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)=>modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
}
