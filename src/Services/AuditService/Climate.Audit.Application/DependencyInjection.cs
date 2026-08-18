using Climate.Audit.Application.Auditing;
using Microsoft.Extensions.DependencyInjection;
namespace Climate.Audit.Application;
public static class DependencyInjection
{
    public static IServiceCollection AddAuditApplication(this IServiceCollection services)=>services.AddScoped<IAuditService,AuditService>();
}
