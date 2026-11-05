using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Climate.ArduinoBridge;

public sealed class AcquisitionWorker(ISerialDataSource source, PersistentQueue queue,
    IOptions<BridgeOptions> options, ILogger<AcquisitionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (string line in source.ReadLinesAsync(stoppingToken))
                {
                    try
                    {
                        TelemetryReading reading = Protocol.Parse(line, options.Value.Device, DateTimeOffset.UtcNow);
                        await queue.EnqueueAsync(reading, stoppingToken);
                    }
                    catch (Exception error) when (error is JsonException or FormatException)
                    { logger.LogWarning("Rejected serial frame: {Reason}", error.Message); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.LogError("Acquisition interrupted: {Reason}. Retrying connection.", error.Message);
                await Task.Delay(TimeSpan.FromSeconds(options.Value.Arduino.ReconnectSeconds), stoppingToken);
            }
        }
    }
}

public sealed class TransmissionWorker(PersistentQueue queue, IHttpClientFactory clients,
    IOptions<BridgeOptions> options, ILogger<TransmissionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        BridgeOptions config = options.Value;
        using HttpClient client = clients.CreateClient("telemetry");
        client.Timeout = TimeSpan.FromSeconds(config.Backend.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("X-Device-Token", config.Backend.DeviceToken);
        Uri endpoint = new(new Uri(config.Backend.BaseUrl), config.Backend.TelemetryPath);
        int failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            TelemetryReading? reading = await queue.PeekAsync(stoppingToken);
            if (reading is null) { await Task.Delay(1000, stoppingToken); continue; }
            if (reading.MeasuredAt < DateTimeOffset.UtcNow.AddHours(-config.Buffer.RetentionHours))
            {
                logger.LogError("Expired buffered reading {ReadingId}; retention policy discards it.", reading.ReadingId);
                await queue.RemoveAsync(reading.ReadingId, stoppingToken);
                continue;
            }
            try
            {
                using HttpResponseMessage response = await client.PostAsJsonAsync(endpoint, reading, Protocol.Json, stoppingToken);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    logger.LogCritical("Device credentials rejected ({Status}); sending stopped. Correct credentials and restart. Buffer retained.", response.StatusCode);
                    return;
                }
                if (response.IsSuccessStatusCode)
                {
                    await queue.RemoveAsync(reading.ReadingId, stoppingToken);
                    failures = 0;
                    logger.LogInformation("Confirmed reading {ReadingId}", reading.ReadingId);
                    continue;
                }
                if ((int)response.StatusCode is >= 400 and < 500 && response.StatusCode != HttpStatusCode.TooManyRequests)
                {
                    logger.LogError("Permanent rejection {Status} for reading {ReadingId}; removing from queue.", response.StatusCode, reading.ReadingId);
                    await queue.RemoveAsync(reading.ReadingId, stoppingToken);
                    continue;
                }
            }
            catch (Exception error) when (error is HttpRequestException || error is TaskCanceledException && !stoppingToken.IsCancellationRequested)
            { logger.LogWarning("Backend unavailable; buffered reading retained."); }
            failures = Math.Min(failures + 1, 6);
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, failures)), stoppingToken);
        }
    }
}
