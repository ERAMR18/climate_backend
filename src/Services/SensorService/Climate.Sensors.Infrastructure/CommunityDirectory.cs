using System.Net;
using System.Net.Http.Json;
using Climate.Sensors.Application.Communities;
using Climate.Sensors.Domain.Communities;
using Microsoft.AspNetCore.Http;

namespace Climate.Sensors.Infrastructure;

public sealed record CommunityDirectoryOptions(bool Enabled, string ApiKey);

public sealed class CommunityDirectory(HttpClient client, CommunityDirectoryOptions options, IHttpContextAccessor context)
{
    public bool Enabled => options.Enabled;
    public async Task<IReadOnlyCollection<Community>> ListAsync(CancellationToken token)
    {
        using HttpRequestMessage request = Request("api/v1/internal/communities");
        using HttpResponseMessage response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        CommunityResponse[] data = await response.Content.ReadFromJsonAsync<CommunityResponse[]>(token)
            ?? throw new HttpRequestException("Community API returned an empty response.");
        return data.Select(ToDomain).ToArray();
    }
    public async Task<Community?> GetAsync(Guid id, CancellationToken token)
    {
        using HttpRequestMessage request = Request($"api/v1/internal/communities/{id}");
        using HttpResponseMessage response = await client.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        CommunityResponse data = await response.Content.ReadFromJsonAsync<CommunityResponse>(token)
            ?? throw new HttpRequestException("Community API returned an empty response.");
        return ToDomain(data);
    }
    private HttpRequestMessage Request(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Internal-Api-Key", options.ApiKey);
        request.Headers.Add("X-Correlation-ID", context.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString());
        return request;
    }
    private static Community ToDomain(CommunityResponse data)
    {
        Community entity = Community.Create(data.Id, data.Name, data.Description, data.Latitude, data.Longitude, data.CreatedAt);
        entity.SetStatus(data.IsActive); entity.SetGeography(data.Municipality, data.Department, data.Country);
        return entity;
    }
}
