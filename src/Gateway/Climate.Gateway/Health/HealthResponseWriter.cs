using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Climate.Gateway.Health;

internal static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context,HealthReport report)
    {
        context.Response.ContentType="application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        { status=report.Status.ToString(),checks=report.Entries.Select(x=>new{name=x.Key,status=x.Value.Status.ToString(),description=x.Value.Description}) }));
    }
}
