using System.Diagnostics;
using System.Text.Json;
using Climate.Contracts.Audit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Climate.BuildingBlocks.Tests;

public sealed class AuditMessagingTests
{
    [Theory]
    [InlineData("AlertRules", "Create", "POST", "CreateAlertRule")]
    [InlineData("AlertRules", "Update", "PUT", "UpdateAlertRule")]
    [InlineData("AlertRules", "Activate", "PATCH", "ActivateAlertRule")]
    [InlineData("AlertRules", "Deactivate", "PATCH", "DeactivateAlertRule")]
    [InlineData("Alerts", "Attend", "PATCH", "AttendAlert")]
    [InlineData("Alerts", "Close", "PATCH", "CloseAlert")]
    public void FutureWorkflowActionsHaveExplicitAuditNames(string controller, string operation, string method, string expected) =>
        Assert.Equal(expected, AuditActions.ForOperation(controller, operation, method));
    [Fact]
    public async Task WriterPreservesActorAndCurrentTrace()
    {
        var capture = new CapturePublisher();
        using var activity = new Activity("update-sensor").Start();
        Guid actor = Guid.NewGuid();
        await new AuditWriter(capture).RecordAsync(actor, "operator", "UpdateSensor", "Sensor", "42", "Updated", "127.0.0.1", CancellationToken.None);
        var message = Assert.IsType<AuditLogRequested>(capture.Message);
        Assert.Equal(actor, message.UserId);
        Assert.Equal(activity.TraceId.ToString(), message.CorrelationId);
        Assert.NotEqual(Guid.Empty, message.EventId);
        Assert.Equal(message, JsonSerializer.Deserialize<AuditLogRequested>(JsonSerializer.Serialize(message)));
    }

    [Fact]
    public async Task BrokerFailureDoesNotFailOrBlockBusinessAuditCall()
    {
        var transport = new FailingTransport();
        using var publisher = new BackgroundAuditPublisher(transport, NullLogger<BackgroundAuditPublisher>.Instance);
        await publisher.StartAsync(CancellationToken.None);
        // Also preserves an event after the caller disconnects following the business commit.
        await new AuditWriter(publisher).RecordAsync(Guid.NewGuid(), "operator", "UpdateSensor", "Sensor", "42", "Updated", null, new CancellationToken(true));
        await transport.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await publisher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void MissingCredentialsFailConfigurationWithoutUsingGuestDefaults()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["RABBITMQ_HOST"] = "broker" }).Build();
        Assert.Throws<InvalidOperationException>(() => RabbitMqAuditOptions.FromConfiguration(configuration));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("invalid")]
    public void InvalidPortIsRejected(string port)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["RABBITMQ_PORT"] = port }).Build();
        Assert.Throws<InvalidOperationException>(() => RabbitMqAuditOptions.FromConfiguration(configuration));
    }

    private sealed class CapturePublisher : IAuditEventPublisher
    {
        public AuditLogRequested? Message { get; private set; }
        public Task PublishAsync(AuditLogRequested auditEvent, CancellationToken cancellationToken)
        { Message = auditEvent; return Task.CompletedTask; }
    }

    private sealed class FailingTransport : IAuditEventTransport
    {
        public TaskCompletionSource Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SendAsync(AuditLogRequested message, CancellationToken token)
        { Attempted.TrySetResult(); throw new IOException("Broker unavailable"); }
    }
}
