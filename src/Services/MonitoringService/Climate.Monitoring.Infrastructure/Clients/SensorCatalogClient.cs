using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Climate.Contracts.Sensors;
using Climate.Monitoring.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Climate.Monitoring.Infrastructure.Clients;

internal sealed class SensorCatalogClient(HttpClient httpClient, IOptions<SensorServiceOptions> options)
    : ISensorCatalogClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyCollection<SensorSummary>> GetActiveSensorsAsync(
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/internal/sensors/active");
        request.Headers.Add("X-Internal-Api-Key", options.Value.ApiKey);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SensorSummary[]>(JsonOptions, cancellationToken) ?? [];
    }
}
