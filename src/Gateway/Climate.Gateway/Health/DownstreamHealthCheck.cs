using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Climate.Gateway.Health;

internal sealed class DownstreamHealthCheck(IHttpClientFactory clientFactory,IConfiguration configuration):IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken cancellationToken=default)
    {
        string[] endpoints=configuration.GetSection("DownstreamHealthEndpoints").Get<string[]>()??[];
        if(endpoints.Length==0)return HealthCheckResult.Unhealthy("No downstream health endpoints are configured.");
        HttpClient client=clientFactory.CreateClient(nameof(DownstreamHealthCheck));
        try
        {
            await Task.WhenAll(endpoints.Select(async endpoint=>
            { using HttpResponseMessage response=await client.GetAsync(endpoint,cancellationToken); response.EnsureSuccessStatusCode(); }));
            return HealthCheckResult.Healthy("All downstream services are reachable.");
        }
        catch(HttpRequestException exception){return HealthCheckResult.Unhealthy("At least one downstream service is unavailable.",exception);}
        catch(TaskCanceledException exception){return HealthCheckResult.Unhealthy("A downstream health check timed out.",exception);}
    }
}
