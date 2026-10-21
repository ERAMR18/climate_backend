namespace Climate.Communities.Api;

public sealed class SensorCountsClient(HttpClient client, IHttpContextAccessor accessor, IConfiguration configuration)
{
    public async Task<Dictionary<Guid, int>> GetAsync(CancellationToken token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "api/v1/internal/sensors/counts");
        request.Headers.Add("X-Internal-Api-Key", configuration["InternalApi:ApiKey"]);
        request.Headers.Add("X-Correlation-ID", accessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString());
        using HttpResponseMessage response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Dictionary<Guid, int>>(token)
            ?? throw new HttpRequestException("Sensor counts response is empty.");
    }
}
