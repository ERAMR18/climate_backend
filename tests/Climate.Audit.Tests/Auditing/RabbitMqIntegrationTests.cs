using System.Text;
using Climate.Audit.Api;
using Climate.Audit.Application;
using Climate.Audit.Infrastructure;
using Climate.Audit.Infrastructure.Persistence;
using Climate.Contracts.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace Climate.Audit.Tests.Auditing;

public sealed class RabbitMqIntegrationTests
{
    [RabbitMqFact]
    public async Task ConfirmedMessagesSurviveConsumerDowntimeDuplicatesAreIgnoredAndMalformedMessagesReachDlq()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:AuditDb"] = "Server=unused;Database=unused" }).Build();
        var options = RabbitMqAuditOptions.FromConfiguration(configuration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var token = timeout.Token;
        await using var connection = await options.CreateFactory().CreateConnectionAsync(token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: token);
        await AuditTopology.DeclareAsync(channel, token);
        // This test requires an isolated broker: never run it against application queues.
        await channel.QueuePurgeAsync(AuditTopology.Queue, token);
        await channel.QueuePurgeAsync(AuditTopology.DeadLetterQueue, token);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditApplication();
        services.AddAuditInfrastructure(configuration);
        services.RemoveAll<DbContextOptions<AuditDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<AuditDbContext>>();
        string database = Path.Combine(Path.GetTempPath(), $"climate-audit-{Guid.NewGuid():N}.db");
        services.AddDbContext<AuditDbContext>(builder => builder.UseSqlite($"Data Source={database};Pooling=False"));
        await using var provider = services.BuildServiceProvider();
        try
        {
            await using (var scope = provider.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.EnsureCreatedAsync(token);
            var message = new AuditLogRequested(Guid.NewGuid(), DateTimeOffset.UtcNow, "test-correlation", Guid.NewGuid(),
                "operator", "UpdateSensor", "Sensor", "42", "Sensor updated", null);
            await using var publisher = new RabbitMqAuditTransport(options);
            await publisher.SendAsync(message, token);
            await publisher.SendAsync(message, token);
            Assert.Equal(2u, await channel.MessageCountAsync(AuditTopology.Queue, token));

            using (var consumer = new AuditConsumer(options, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AuditConsumer>.Instance))
            {
                await consumer.StartAsync(token);
                await WaitForEmptyAsync(channel, token);
                await consumer.StopAsync(token);
            }
            await using (var scope = provider.CreateAsyncScope())
            {
                var rows = await scope.ServiceProvider.GetRequiredService<AuditDbContext>().AuditLogs.ToArrayAsync(token);
                Assert.Single(rows);
                Assert.Equal(message.EventId, rows[0].Id);
                Assert.Equal(message.CorrelationId, rows[0].CorrelationId);
            }

            await publisher.SendAsync(message with { EventId = Guid.NewGuid() }, token);
            Assert.Equal(1u, await channel.MessageCountAsync(AuditTopology.Queue, token));
            using (var restarted = new AuditConsumer(options, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AuditConsumer>.Instance))
            {
                await restarted.StartAsync(token);
                await channel.BasicPublishAsync(AuditTopology.Exchange, AuditTopology.RoutingKey,
                    mandatory: true, basicProperties: new BasicProperties { Persistent = true }, body: Encoding.UTF8.GetBytes("not-json"), cancellationToken: token);
                while (await channel.MessageCountAsync(AuditTopology.DeadLetterQueue, token) == 0)
                    await Task.Delay(100, token);
                await restarted.StopAsync(token);
            }
            Assert.Equal(1u, await channel.MessageCountAsync(AuditTopology.DeadLetterQueue, token));
            await using var finalScope = provider.CreateAsyncScope();
            Assert.Equal(2, await finalScope.ServiceProvider.GetRequiredService<AuditDbContext>().AuditLogs.CountAsync(token));
        }
        finally { File.Delete(database); }
    }

    private static async Task WaitForEmptyAsync(IChannel channel, CancellationToken token)
    {
        // Ready count excludes the in-flight message. StopAsync waits for its persistence/ACK.
        while (await channel.MessageCountAsync(AuditTopology.Queue, token) > 0) await Task.Delay(100, token);
        await Task.Delay(500, token);
    }
}

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CLIMATE_RABBITMQ_INTEGRATION") != "1")
            Skip = "Requires an isolated RabbitMQ broker and CLIMATE_RABBITMQ_INTEGRATION=1.";
    }
}
