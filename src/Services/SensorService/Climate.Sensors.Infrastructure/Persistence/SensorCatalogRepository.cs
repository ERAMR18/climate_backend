using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Domain.Communities;
using Climate.Sensors.Domain.Sensors;
using Microsoft.EntityFrameworkCore;

namespace Climate.Sensors.Infrastructure.Persistence;

internal sealed class SensorCatalogRepository(SensorsDbContext dbContext) : ISensorCatalogRepository
{
    public async Task<IReadOnlyCollection<Community>> ListCommunitiesAsync(CancellationToken cancellationToken) =>
        await dbContext.Communities.AsNoTracking().OrderBy(community => community.Name).ToArrayAsync(cancellationToken);

    public Task<Community?> GetCommunityAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Communities.SingleOrDefaultAsync(community => community.Id == id, cancellationToken);

    public Task<bool> CommunityNameExistsAsync(
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        string normalizedName = name.Trim();
        return dbContext.Communities.AnyAsync(
            community => community.Name == normalizedName &&
                         (!excludingId.HasValue || community.Id != excludingId.Value),
            cancellationToken);
    }

    public async Task AddCommunityAsync(Community community, CancellationToken cancellationToken) =>
        await dbContext.Communities.AddAsync(community, cancellationToken);

    public async Task<IReadOnlyCollection<Sensor>> ListSensorsAsync(CancellationToken cancellationToken) =>
        await dbContext.Sensors
            .AsNoTracking()
            .Include(sensor => sensor.Community)
            .OrderBy(sensor => sensor.Code)
            .ToArrayAsync(cancellationToken);

    public Task<Sensor?> GetSensorAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Sensors.Include(sensor => sensor.Community)
            .SingleOrDefaultAsync(sensor => sensor.Id == id, cancellationToken);

    public Task<bool> SensorCodeExistsAsync(
        string code,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();
        return dbContext.Sensors.AnyAsync(
            sensor => sensor.NormalizedCode == normalizedCode &&
                      (!excludingId.HasValue || sensor.Id != excludingId.Value),
            cancellationToken);
    }

    public async Task AddSensorAsync(Sensor sensor, CancellationToken cancellationToken) =>
        await dbContext.Sensors.AddAsync(sensor, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
