using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Climate.Audit.Infrastructure.Persistence;
public static class AuditDatabaseInitializer
{
    public static async Task InitializeAuditDatabaseAsync(this IServiceProvider services,CancellationToken ct)
    { await using AsyncServiceScope scope=services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.MigrateAsync(ct); }
}
