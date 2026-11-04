using System.Security.Cryptography;
using System.Text;
using Climate.Contracts.Realtime;
using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Application.Readings;
using Climate.Monitoring.Domain.Readings;
using Climate.Monitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Climate.Monitoring.Api.Controllers;

[ApiController]
[Route("api/v1/monitoring/telemetry")]
public sealed class TelemetryController(MonitoringDbContext database, ISensorCatalogClient sensors,
    IOptions<TelemetryOptions> options) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous] // Dedicated device credential; human JWTs never authorize ingestion.
    public async Task<IActionResult> Ingest(TelemetryRequest request, CancellationToken cancellationToken)
    {
        TelemetryDevice? device = options.Value.Devices.SingleOrDefault(item => item.DeviceId == request.DeviceId);
        string credential = Request.Headers["X-Device-Token"].ToString();
        if (device is null || !device.IsActive || credential.Length is < 32 or > 512) return Unauthorized();
        byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(credential));
        byte[] expected;
        try { expected = Convert.FromHexString(device.TokenSha256); }
        catch (FormatException) { return Unauthorized(); }
        if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return Unauthorized();
        if (device.SensorId != request.SensorId || device.Source != request.Source) return Forbid();
        if (request.ReadingId == Guid.Empty || request.Unit != "C"
            || decimal.Round(request.Value, 4) != request.Value
            || request.Value < options.Value.MinimumTemperature || request.Value > options.Value.MaximumTemperature
            || request.MeasuredAt < DateTimeOffset.UtcNow.AddDays(-7) || request.MeasuredAt > DateTimeOffset.UtcNow.AddMinutes(5))
            return BadRequest(new { message = "Invalid reading identifier, unit, temperature or timestamp." });
        Climate.Contracts.Sensors.SensorSummary? sensor = (await sensors.GetActiveSensorsAsync(cancellationToken))
            .SingleOrDefault(item => item.Id == device.SensorId && item.IsActive);
        if (sensor is null || sensor.Type != Climate.Contracts.Sensors.SensorType.Temperature
            || sensor.Unit is not ("C" or "°C")) return BadRequest(new { message = "Temperature sensor missing, inactive or incompatible." });

        // Reuse the audit filter's transaction. Range locks serialize UUID lookup+insert.
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken) : null;
        SensorReading? existing = await database.Readings
            .FromSqlInterpolated($"SELECT * FROM [SensorReadings] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {request.ReadingId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.SensorId != request.SensorId || existing.DeviceId != request.DeviceId
                || existing.Value != request.Value || existing.Source != request.Source
                || existing.RecordedAt.ToUnixTimeMilliseconds() != request.MeasuredAt.ToUnixTimeMilliseconds())
                return Conflict(new { message = "Reading identifier already used with different data." });
            return Ok(SensorReadingResponse.FromEntity(existing));
        }
        SensorReading reading = SensorReading.Create(request.ReadingId, sensor.Id, sensor.CommunityId,
            SensorType.Temperature, request.Value, sensor.Unit, request.MeasuredAt);
        reading.SetTelemetryOrigin(request.Source, request.DeviceId);
        await database.Readings.AddAsync(reading, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        SensorReadingResponse response = SensorReadingResponse.FromEntity(reading);
        return Ok(response);
    }
}
