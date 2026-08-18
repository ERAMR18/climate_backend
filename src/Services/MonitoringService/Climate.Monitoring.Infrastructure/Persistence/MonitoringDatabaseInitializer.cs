using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Monitoring.Infrastructure.Persistence;

public static class MonitoringDatabaseInitializer
{
    public static async Task InitializeMonitoringDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        MonitoringDbContext dbContext = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
