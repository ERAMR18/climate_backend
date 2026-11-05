using System.Net;
using System.Text.Json;
using Climate.ArduinoBridge;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Climate.ArduinoBridge.Tests;

public sealed class BridgeTests
{
    private static BridgeOptions Configuration() => new()
    {
        Device = new() { DeviceId = "test-device", SensorId = Guid.NewGuid().ToString() },
        Backend = new() { BaseUrl = "http://localhost:8080", AllowLocalHttp = true, DeviceToken = new string('x', 32) },
        Buffer = new() { Path = Path.Combine(Path.GetTempPath(), $"bridge-test-{Guid.NewGuid()}.db"), MaxReadings = 2 }
    };
    private static string Frame(string device = "test-device", decimal value = 27, string unit = "C") =>
        JsonSerializer.Serialize(new SerialReading(1, device, "TEMPERATURE", value, unit, 1, null), Protocol.Json);

    [Fact] public void ValidFrameUsesReceptionTime()
    {
        BridgeOptions config = Configuration();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TelemetryReading reading = Protocol.Parse(Frame(), config.Device, now);
        Assert.Equal(now, reading.MeasuredAt);
        Assert.NotEqual(Guid.Empty, reading.ReadingId);
        Assert.Equal(27, reading.Value);
    }
    [Theory]
    [InlineData("other", 27, "C")]
    [InlineData("test-device", 151, "C")]
    [InlineData("test-device", -56, "C")]
    [InlineData("test-device", 27, "F")]
    public void RejectsInvalidFrame(string device, decimal value, string unit) =>
        Assert.Throws<FormatException>(() => Protocol.Parse(Frame(device, value, unit), Configuration().Device, DateTimeOffset.UtcNow));
    [Fact] public void RejectsMalformedJson() => Assert.Throws<JsonException>(() => Protocol.Parse("{broken", Configuration().Device, DateTimeOffset.UtcNow));
    [Fact] public void MissingValueCannotSilentlyBecomeZero() => Assert.Throws<JsonException>(() => Protocol.Parse(
        "{\"protocolVersion\":1,\"deviceId\":\"test-device\",\"sensorType\":\"TEMPERATURE\",\"unit\":\"C\",\"sequence\":1}",
        Configuration().Device, DateTimeOffset.UtcNow));
    [Fact] public void RejectsRemoteHttp()
    {
        BridgeOptions config = Configuration();
        Assert.True(config.IsValid());
        config.Backend.BaseUrl = "http://example.com";
        Assert.False(config.IsValid());
    }
    [Fact] public async Task QueueSurvivesRestartAndDeduplicatesUuid()
    {
        BridgeOptions config = Configuration();
        PersistentQueue queue = new(Options.Create(config));
        TelemetryReading reading = Protocol.Parse(Frame(), config.Device, DateTimeOffset.UtcNow);
        await queue.EnqueueAsync(reading, default);
        await queue.EnqueueAsync(reading, default);
        PersistentQueue reopened = new(Options.Create(config));
        Assert.Equal(reading, await reopened.PeekAsync(default));
        await reopened.RemoveAsync(reading.ReadingId, default);
        Assert.Null(await queue.PeekAsync(default));
    }
    [Fact] public async Task QueueIsBoundedWithoutSilentlyEvictingOldReadings()
    {
        BridgeOptions config = Configuration();
        PersistentQueue queue = new(Options.Create(config));
        TelemetryReading first = Protocol.Parse(Frame(), config.Device, DateTimeOffset.UtcNow);
        await queue.EnqueueAsync(first, default);
        await queue.EnqueueAsync(first with { ReadingId = Guid.NewGuid() }, default);
        await Assert.ThrowsAsync<IOException>(() => queue.EnqueueAsync(first with { ReadingId = Guid.NewGuid() }, default));
        Assert.Equal(first.ReadingId, (await queue.PeekAsync(default))!.ReadingId);
    }
    [Fact] public async Task AcquisitionReconnectsAfterSerialDisconnection()
    {
        BridgeOptions config = Configuration();
        config.Arduino.ReconnectSeconds = 1;
        PersistentQueue queue = new(Options.Create(config));
        ReconnectingSource source = new();
        using AcquisitionWorker worker = new(source, queue, Options.Create(config), NullLogger<AcquisitionWorker>.Instance);
        await worker.StartAsync(default);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (await queue.PeekAsync(timeout.Token) is null) await Task.Delay(50, timeout.Token);
        await worker.StopAsync(default);
        Assert.Equal(2, source.Connections);
    }
    [Fact] public async Task UnauthorizedStopsTransmissionAndRetainsBuffer()
    {
        BridgeOptions config = Configuration();
        PersistentQueue queue = new(Options.Create(config));
        TelemetryReading reading = Protocol.Parse(Frame(), config.Device, DateTimeOffset.UtcNow);
        await queue.EnqueueAsync(reading, default);
        Handler handler = new(HttpStatusCode.Unauthorized);
        using TransmissionWorker worker = new(queue, new ClientFactory(handler), Options.Create(config), NullLogger<TransmissionWorker>.Instance);
        await worker.StartAsync(default);
        await worker.ExecuteTask!;
        Assert.Equal(reading.ReadingId, (await queue.PeekAsync(default))!.ReadingId);
        Assert.Equal(1, handler.Calls);
    }
    [Fact] public async Task BackendFailureRetainsStableUuidThenSuccessRemovesIt()
    {
        BridgeOptions config = Configuration();
        PersistentQueue queue = new(Options.Create(config));
        TelemetryReading reading = Protocol.Parse(Frame(), config.Device, DateTimeOffset.UtcNow);
        await queue.EnqueueAsync(reading, default);
        Handler handler = new(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        using TransmissionWorker worker = new(queue, new ClientFactory(handler), Options.Create(config), NullLogger<TransmissionWorker>.Instance);
        await worker.StartAsync(default);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        while (await queue.PeekAsync(timeout.Token) is not null) await Task.Delay(50, timeout.Token);
        await worker.StopAsync(default);
        Assert.Equal(2, handler.Calls);
        Assert.All(handler.Bodies, body => Assert.Contains(reading.ReadingId.ToString(), body));
    }
    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, false); }
    private sealed class ReconnectingSource : ISerialDataSource
    {
        public int Connections { get; private set; }
        public async IAsyncEnumerable<string> ReadLinesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Connections++;
            if (Connections == 1) throw new IOException("Disconnected serial port.");
            yield return Frame();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
    private sealed class Handler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<string> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.True(request.Headers.Contains("X-Device-Token"));
            Bodies.Add(await request.Content!.ReadAsStringAsync(token));
            return new HttpResponseMessage(statuses[Math.Min(Calls++, statuses.Length - 1)]);
        }
    }
}
