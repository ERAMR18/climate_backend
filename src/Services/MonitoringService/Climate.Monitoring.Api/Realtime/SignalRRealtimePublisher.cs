using Climate.Monitoring.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;
namespace Climate.Monitoring.Api.Realtime;
internal sealed class SignalRRealtimePublisher(IHubContext<MonitoringHub> hub):IRealtimePublisher
{
    public Task PublishAsync(string eventName,object payload,CancellationToken cancellationToken)=>hub.Clients.All.SendAsync(eventName,payload,cancellationToken);
}
