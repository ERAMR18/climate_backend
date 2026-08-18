using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Events.Infrastructure.Persistence;

public static class EventsDatabaseInitializer
{
    public static async Task InitializeEventsDatabaseAsync(this IServiceProvider services, CancellationToken token)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database.MigrateAsync(token);
    }
}
