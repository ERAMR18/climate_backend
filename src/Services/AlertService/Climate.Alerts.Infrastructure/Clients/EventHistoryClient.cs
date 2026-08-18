using System.Net.Http.Json;
using Climate.Alerts.Application.Abstractions;
using Climate.Alerts.Domain.Alerts;
using Microsoft.Extensions.Options;

namespace Climate.Alerts.Infrastructure.Clients;

internal sealed class EventHistoryClient(HttpClient client, IOptions<EventServiceOptions> options) : IEventHistoryClient
{
    public async Task RecordAsync(ClimateAlert alert, CancellationToken token)
    {
        var payload = new
        {
            EventId = alert.Id,
            AlertId = alert.Id,
            alert.SensorId,
            alert.CommunityId,
            RiskType = alert.AlertType.ToString(),
            AlertLevel = alert.Level.ToString(),
            alert.Description,
            OccurredAt = alert.GeneratedAt,
            alert.ResolvedAt
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/internal/events") { Content = JsonContent.Create(payload) };
        request.Headers.Add("X-Internal-Api-Key", options.Value.ApiKey);
        using HttpResponseMessage response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
    }
}
