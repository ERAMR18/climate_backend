using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Climate.Contracts.Audit;

public static class AuditMessagingExtensions
{
    public static IServiceCollection AddAuditOutbox<TContext>(this IServiceCollection services, IConfiguration configuration) where TContext : DbContext
    {
        services.AddSingleton(RabbitMqAuditOptions.FromConfiguration(configuration));
        services.AddSingleton<IAuditEventTransport, RabbitMqAuditTransport>();
        services.AddScoped<IAuditEventPublisher, OutboxAuditPublisher<TContext>>();
        services.AddScoped<AuditWriter>();
        services.AddScoped<AuditTransactionFilter<TContext>>();
        services.AddControllers(options => options.Filters.AddService<AuditTransactionFilter<TContext>>());
        if (!configuration.GetValue<bool>("OpenApi:ExportOnly")) services.AddHostedService<OutboxAuditWorker<TContext>>();
        return services;
    }
}
