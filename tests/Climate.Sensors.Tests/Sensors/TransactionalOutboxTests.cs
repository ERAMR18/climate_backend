using System.Security.Claims;
using System.Data.Common;
using Climate.Contracts.Audit;
using Climate.Contracts.Realtime;
using Climate.Sensors.Api.Controllers;
using Climate.Sensors.Application;
using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Application.Sensors;
using Climate.Sensors.Domain.Communities;
using Climate.Sensors.Domain.Sensors;
using Climate.Sensors.Infrastructure;
using Climate.Sensors.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Climate.Sensors.Tests.Sensors;

public sealed class TransactionalOutboxTests
{
    [Fact]
    public async Task CatalogFiltersRunInSqlAndCommunityCountsUseOneQuery()
    {
        string database = Path.Combine(Path.GetTempPath(), $"climate-filter-{Guid.NewGuid():N}.db");
        try
        {
            var commands = new QueryCounter();
            await using var provider = CreateProvider(database, commands);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SensorsDbContext>();
            await db.Database.EnsureCreatedAsync();
            var town = Community.Create(Guid.NewGuid(), "Town", "Test", 14, -90, DateTimeOffset.UtcNow);
            town.SetGeography("Municipality", "Department", "Country");
            var other = Community.Create(Guid.NewGuid(), "Other", null, 14, -90, DateTimeOffset.UtcNow);
            var active = Sensor.Create(Guid.NewGuid(), "River", "RIVER-1", null, SensorType.RiverLevel, "m", town.Id, 14, -90, DateTimeOffset.UtcNow);
            active.SetInstallation(new DateOnly(2026, 1, 1), "Bridge", null);
            var inactive = Sensor.Create(Guid.NewGuid(), "Smoke", "SMOKE-1", null, SensorType.Smoke, "%", town.Id, 14, -90, DateTimeOffset.UtcNow);
            inactive.SetStatus(false, DateTimeOffset.UtcNow);
            db.Communities.AddRange(town, other); db.Sensors.AddRange(active, inactive); await db.SaveChangesAsync();
            var repository = scope.ServiceProvider.GetRequiredService<ISensorCatalogRepository>();
            commands.Reads = 0;
            var communities = await repository.SearchCommunitiesAsync("Town", true, "Municipality", "Department", CancellationToken.None);
            Assert.Single(communities); Assert.Equal(2, communities.Single().SensorCount); Assert.Equal(1, commands.Reads);
            var sensors = await repository.SearchSensorsAsync(town.Id, SensorType.RiverLevel, true, "river", "River", CancellationToken.None);
            Assert.Single(sensors); Assert.Equal(active.Id, sensors.Single().Id); Assert.Equal("Bridge", sensors.Single().Location);
            Assert.Empty(await repository.SearchSensorsAsync(town.Id, SensorType.Smoke, true, null, null, CancellationToken.None));
        }
        finally { File.Delete(database); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusinessAndAuditCommitTogetherOrBothRollback(bool fail)
    {
        string database = Path.Combine(Path.GetTempPath(), $"climate-outbox-{Guid.NewGuid():N}.db");
        try
        {
            await using var provider = CreateProvider(database);
            Guid communityId = Guid.NewGuid(), sensorId = Guid.NewGuid();
            await using (var seed = provider.CreateAsyncScope())
            {
                var db = seed.ServiceProvider.GetRequiredService<SensorsDbContext>();
                await db.Database.EnsureCreatedAsync();
                db.Communities.Add(Community.Create(communityId, "Town", null, 14, -90, DateTimeOffset.UtcNow));
                db.Sensors.Add(Sensor.Create(sensorId, "Original", "TEMP-1", null, SensorType.Temperature, "C", communityId, 14, -90, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
            }
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SensorsDbContext>();
                var writer = new AuditWriter(new OutboxAuditPublisher<SensorsDbContext>(db));
                var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Name, "admin")], "test")) };
                http.Request.Method = "PUT";
                var route = new RouteData(); route.Values["controller"] = "Sensors"; route.Values["id"] = sensorId;
                var action = new ActionContext(http, route, new ControllerActionDescriptor());
                var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
                using var client = new HttpClient();
                var controller = new SensorsController(scope.ServiceProvider.GetRequiredService<ISensorService>(), writer, new RealtimeWriter(client, "unused"))
                { ControllerContext = new ControllerContext(action) };
                await new AuditTransactionFilter<SensorsDbContext>(db, writer).OnActionExecutionAsync(context, async () =>
                {
                    var response = await controller.Update(sensorId, new("Updated", "TEMP-1", null, SensorType.Temperature, "C", communityId, 14, -90), CancellationToken.None);
                    Assert.IsType<OkObjectResult>(response.Result);
                    return new ActionExecutedContext(action, [], controller) { Result = fail ? new BadRequestResult() : response.Result };
                });
            }
            // New scope/process-facing state proves durable storage, not a queued object in memory.
            await using (var verify = provider.CreateAsyncScope())
            {
                var db = verify.ServiceProvider.GetRequiredService<SensorsDbContext>();
                Assert.Equal(fail ? "Original" : "Updated", (await db.Sensors.SingleAsync()).Name);
                Assert.Equal(fail ? 0 : 1, await db.Set<AuditOutboxMessage>().CountAsync());
            }
            if (fail) return;
            var transport = new RecoverableTransport();
            using (var down = new OutboxAuditWorker<SensorsDbContext>(provider.GetRequiredService<IServiceScopeFactory>(), transport, NullLogger<OutboxAuditWorker<SensorsDbContext>>.Instance))
                await Assert.ThrowsAsync<IOException>(() => down.DispatchOnceAsync(CancellationToken.None));
            // A fresh provider and worker resume the same database after a simulated producer restart.
            await using var restartedProvider = CreateProvider(database);
            transport.Available = true;
            using var restarted = new OutboxAuditWorker<SensorsDbContext>(restartedProvider.GetRequiredService<IServiceScopeFactory>(), transport, NullLogger<OutboxAuditWorker<SensorsDbContext>>.Instance);
            await restarted.DispatchOnceAsync(CancellationToken.None);
            await restarted.DispatchOnceAsync(CancellationToken.None);
            Assert.Single(transport.Delivered);
            await using var final = restartedProvider.CreateAsyncScope();
            Assert.NotNull((await final.ServiceProvider.GetRequiredService<SensorsDbContext>().Set<AuditOutboxMessage>().SingleAsync()).PublishedAt);
        }
        finally { File.Delete(database); }
    }

    private static ServiceProvider CreateProvider(string database, QueryCounter? commands = null)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddSensorsApplication();
        services.AddSensorsInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:SensorDb"] = "Server=unused" }).Build());
        services.RemoveAll<DbContextOptions<SensorsDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<SensorsDbContext>>();
        services.AddDbContext<SensorsDbContext>(x => { x.UseSqlite($"Data Source={database};Pooling=False"); if (commands is not null) x.AddInterceptors(commands); });
        return services.BuildServiceProvider();
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Reads { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Reads++; return ValueTask.FromResult(result); }
    }

    private sealed class RecoverableTransport : IAuditEventTransport
    {
        public bool Available { get; set; }
        public List<AuditLogRequested> Delivered { get; } = [];
        public Task SendAsync(AuditLogRequested message, CancellationToken token)
        { if (!Available) throw new IOException("RabbitMQ down"); Delivered.Add(message); return Task.CompletedTask; }
    }
}
