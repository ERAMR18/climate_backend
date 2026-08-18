using Climate.Audit.Application.Abstractions;
using Climate.Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Climate.Audit.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection s,IConfiguration c)
    { string cs=c.GetConnectionString("AuditDb")??throw new InvalidOperationException("Connection string 'AuditDb' is required.");
      s.AddDbContext<AuditDbContext>(x=>x.UseSqlServer(cs)); s.AddHealthChecks().AddDbContextCheck<AuditDbContext>("audit-database");
      s.AddScoped<IAuditLogRepository,AuditLogRepository>(); return s; }
}
