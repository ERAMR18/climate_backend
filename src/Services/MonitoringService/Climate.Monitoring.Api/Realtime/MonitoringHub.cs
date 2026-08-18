using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace Climate.Monitoring.Api.Realtime;
[Authorize]
public sealed class MonitoringHub:Hub;
