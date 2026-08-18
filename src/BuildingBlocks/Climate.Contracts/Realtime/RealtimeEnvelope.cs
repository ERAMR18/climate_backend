using System.Net.Http.Json;

namespace Climate.Contracts.Realtime;

public sealed record RealtimeEnvelope(string EventName, object Payload);

public sealed class RealtimeWriter(HttpClient httpClient, string apiKey)
{
    public async Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"api/v1/internal/realtime")
        { Content=JsonContent.Create(new RealtimeEnvelope(eventName,payload)) };
        request.Headers.Add("X-Internal-Api-Key",apiKey);
        using HttpResponseMessage response=await httpClient.SendAsync(request,cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
