using System.Net.Http.Json;
using Climate.Alerts.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Climate.Alerts.Infrastructure.Clients;

internal sealed class SensorActivityClient(HttpClient client, IConfiguration configuration) : ISensorActivityClient
{
    public async Task<bool> IsActiveAsync(Guid sensorId, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/internal/sensors/{sensorId}/active");
        request.Headers.Add("X-Internal-Api-Key", configuration["SensorService:ApiKey"]);
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(token);
    }
}
