using System.Net.Http.Json;
using Climate.Contracts.Monitoring;
using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Domain.Readings;
using Microsoft.Extensions.Options;

namespace Climate.Monitoring.Infrastructure.Clients;

internal sealed class AlertEvaluationClient(HttpClient httpClient, IOptions<AlertServiceOptions> options)
    : IAlertEvaluationClient
{
    public async Task EvaluateAsync(SensorReading reading, CancellationToken cancellationToken)
    {
        var integrationEvent = new SensorReadingRecorded(
            Guid.NewGuid(),
            reading.RecordedAt,
            reading.Id.ToString(),
            reading.Id,
            reading.SensorId,
            reading.CommunityId,
            (Climate.Contracts.Sensors.SensorType)reading.SensorType,
            reading.Value,
            reading.Unit,
            reading.RecordedAt);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/internal/risk-evaluations")
        {
            Content = JsonContent.Create(integrationEvent)
        };
        request.Headers.Add("X-Internal-Api-Key", options.Value.ApiKey);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
