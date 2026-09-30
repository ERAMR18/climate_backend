using System.Security.Claims;
using Climate.Contracts.Audit;
using Climate.Contracts.Realtime;
using Climate.Sensors.Api.Controllers;
using Climate.Sensors.Application;
using Climate.Sensors.Application.Sensors;
using Climate.Sensors.Domain.Communities;
using Climate.Sensors.Domain.Sensors;
using Climate.Sensors.Infrastructure;
using Climate.Sensors.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Climate.Sensors.Tests.Sensors;

public sealed class AuditDecouplingTests
{
    [Fact]
    public async Task UpdateReturnsSuccessAndPersistsWhenAuditTransportIsUnavailable()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSensorsApplication();
        services.AddSensorsInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:SensorDb"] = "Server=unused;Database=unused" }).Build());
        services.RemoveAll<DbContextOptions<SensorsDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<SensorsDbContext>>();
        string database = Guid.NewGuid().ToString();
        services.AddDbContext<SensorsDbContext>(options => options.UseInMemoryDatabase(database));
        await using var provider = services.BuildServiceProvider();
        Guid communityId = Guid.NewGuid();
        Guid sensorId = Guid.NewGuid();
        await using (var seed = provider.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<SensorsDbContext>();
            db.Communities.Add(Community.Create(communityId, "Community", null, 14m, -90m, DateTimeOffset.UtcNow));
            db.Sensors.Add(Sensor.Create(sensorId, "Original", "TEMP-1", null, SensorType.Temperature, "C", communityId, 14m, -90m, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        var transport = new UnavailableTransport();
        using var publisher = new BackgroundAuditPublisher(transport, NullLogger<BackgroundAuditPublisher>.Instance);
        await publisher.StartAsync(CancellationToken.None);
        using var realtimeHttp = new HttpClient();
        await using (var scope = provider.CreateAsyncScope())
        {
            var controller = new SensorsController(scope.ServiceProvider.GetRequiredService<ISensorService>(),
                new AuditWriter(publisher), new RealtimeWriter(realtimeHttp, "unused"))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Name, "operator")], "test"))
                } }
            };
            var result = await controller.Update(sensorId,
                new UpdateSensorRequest("Updated", "TEMP-1", null, SensorType.Temperature, "C", communityId, 14m, -90m), CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);
        }
        await transport.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await publisher.StopAsync(CancellationToken.None);
        await using var verification = provider.CreateAsyncScope();
        Assert.Equal("Updated", (await verification.ServiceProvider.GetRequiredService<SensorsDbContext>().Sensors.SingleAsync()).Name);
    }

    private sealed class UnavailableTransport : IAuditEventTransport
    {
        public TaskCompletionSource Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SendAsync(AuditLogRequested message, CancellationToken token)
        { Attempted.TrySetResult(); throw new IOException("Audit broker unavailable; Audit Service is not running"); }
    }
}
