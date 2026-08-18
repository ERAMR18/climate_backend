using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Alerts.Infrastructure.Persistence;

public static class AlertsDatabaseInitializer
{
    public static async Task InitializeAlertsDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        AlertsDbContext dbContext = scope.ServiceProvider.GetRequiredService<AlertsDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
