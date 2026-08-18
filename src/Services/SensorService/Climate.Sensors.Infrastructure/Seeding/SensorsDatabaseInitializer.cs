using Climate.Sensors.Domain.Communities;
using Climate.Sensors.Domain.Sensors;
using Climate.Sensors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Climate.Sensors.Infrastructure.Seeding;

public static class SensorsDatabaseInitializer
{
    public static async Task InitializeSensorsDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        SensorsDbContext dbContext = scope.ServiceProvider.GetRequiredService<SensorsDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        DemoSeedOptions options = scope.ServiceProvider.GetRequiredService<IOptions<DemoSeedOptions>>().Value;
        if (!options.Enabled)
        {
            return;
        }

        DateTimeOffset now = TimeProvider.System.GetUtcNow();
        Community[] communities =
        [
            CreateCommunity("10000000-0000-0000-0000-000000000001", "Ciudad de Guatemala", "Estación urbana central.", 14.6349m, -90.5069m, now),
            CreateCommunity("10000000-0000-0000-0000-000000000002", "Puerto Barrios", "Estación costera del Caribe.", 15.7278m, -88.5944m, now),
            CreateCommunity("10000000-0000-0000-0000-000000000003", "Quetzaltenango", "Estación del altiplano occidental.", 14.8347m, -91.5181m, now)
        ];

        Sensor[] sensors =
        [
            CreateSensor("20000000-0000-0000-0000-000000000001", "Temperatura capital", "GUA-TEMP-01", SensorType.Temperature, "°C", communities[0].Id, 14.6349m, -90.5069m, now),
            CreateSensor("20000000-0000-0000-0000-000000000002", "Humedad capital", "GUA-HUM-01", SensorType.Humidity, "%", communities[0].Id, 14.6349m, -90.5069m, now),
            CreateSensor("20000000-0000-0000-0000-000000000003", "Viento capital", "GUA-WIND-01", SensorType.WindSpeed, "km/h", communities[0].Id, 14.6349m, -90.5069m, now),
            CreateSensor("20000000-0000-0000-0000-000000000004", "Lluvia capital", "GUA-RAIN-01", SensorType.Rainfall, "mm", communities[0].Id, 14.6349m, -90.5069m, now),
            CreateSensor("20000000-0000-0000-0000-000000000005", "Nivel río Las Vacas", "GUA-WATER-01", SensorType.WaterLevel, "m", communities[0].Id, 14.6760m, -90.4720m, now),
            CreateSensor("20000000-0000-0000-0000-000000000006", "Temperatura Caribe", "PBR-TEMP-01", SensorType.Temperature, "°C", communities[1].Id, 15.7278m, -88.5944m, now),
            CreateSensor("20000000-0000-0000-0000-000000000007", "Humedad Caribe", "PBR-HUM-01", SensorType.Humidity, "%", communities[1].Id, 15.7278m, -88.5944m, now),
            CreateSensor("20000000-0000-0000-0000-000000000008", "Viento Caribe", "PBR-WIND-01", SensorType.WindSpeed, "km/h", communities[1].Id, 15.7278m, -88.5944m, now),
            CreateSensor("20000000-0000-0000-0000-000000000009", "Lluvia Caribe", "PBR-RAIN-01", SensorType.Rainfall, "mm", communities[1].Id, 15.7278m, -88.5944m, now),
            CreateSensor("20000000-0000-0000-0000-000000000010", "Nivel río Motagua", "PBR-WATER-01", SensorType.WaterLevel, "m", communities[1].Id, 15.7170m, -88.6120m, now),
            CreateSensor("20000000-0000-0000-0000-000000000011", "Temperatura altiplano", "XELA-TEMP-01", SensorType.Temperature, "°C", communities[2].Id, 14.8347m, -91.5181m, now),
            CreateSensor("20000000-0000-0000-0000-000000000012", "Humedad altiplano", "XELA-HUM-01", SensorType.Humidity, "%", communities[2].Id, 14.8347m, -91.5181m, now),
            CreateSensor("20000000-0000-0000-0000-000000000013", "Viento altiplano", "XELA-WIND-01", SensorType.WindSpeed, "km/h", communities[2].Id, 14.8347m, -91.5181m, now),
            CreateSensor("20000000-0000-0000-0000-000000000014", "Lluvia altiplano", "XELA-RAIN-01", SensorType.Rainfall, "mm", communities[2].Id, 14.8347m, -91.5181m, now),
            CreateSensor("20000000-0000-0000-0000-000000000015", "Nivel río Samalá", "XELA-WATER-01", SensorType.WaterLevel, "m", communities[2].Id, 14.8100m, -91.5400m, now)
        ];

        HashSet<Guid> existingCommunityIds = (await dbContext.Communities
            .Select(community => community.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        HashSet<string> existingSensorCodes = (await dbContext.Sensors
            .Select(sensor => sensor.NormalizedCode)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<Guid> existingSensorIds = (await dbContext.Sensors
            .Select(sensor => sensor.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        await dbContext.Communities.AddRangeAsync(
            communities.Where(community => !existingCommunityIds.Contains(community.Id)),
            cancellationToken);
        await dbContext.Sensors.AddRangeAsync(
            sensors.Where(sensor =>
                !existingSensorIds.Contains(sensor.Id) &&
                !existingSensorCodes.Contains(sensor.NormalizedCode)),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Community CreateCommunity(
        string id,
        string name,
        string description,
        decimal latitude,
        decimal longitude,
        DateTimeOffset now) =>
        Community.Create(Guid.Parse(id), name, description, latitude, longitude, now);

    private static Sensor CreateSensor(
        string id,
        string name,
        string code,
        SensorType type,
        string unit,
        Guid communityId,
        decimal latitude,
        decimal longitude,
        DateTimeOffset now) =>
        Sensor.Create(
            Guid.Parse(id),
            name,
            code,
            "Sensor simulado para el escenario inicial.",
            type,
            unit,
            communityId,
            latitude,
            longitude,
            now);
}
