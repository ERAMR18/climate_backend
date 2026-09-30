using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Domain.Communities;
using Climate.Sensors.Domain.Sensors;
using Microsoft.EntityFrameworkCore;

namespace Climate.Sensors.Infrastructure.Persistence;

internal sealed class SensorCatalogRepository(SensorsDbContext dbContext) : ISensorCatalogRepository
{
    public Task<int> CountSensorsAsync(Guid communityId, CancellationToken token) => dbContext.Sensors.CountAsync(x => x.CommunityId == communityId, token);
    public async Task<IReadOnlyCollection<Climate.Sensors.Application.Communities.CommunityResponse>> SearchCommunitiesAsync(string? search, bool? isActive, string? municipality, string? department, CancellationToken token)
    {
        var q = dbContext.Communities.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); q = q.Where(x => x.Name.Contains(term) || (x.Description != null && x.Description.Contains(term))); }
        if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(municipality)) q = q.Where(x => x.Municipality == municipality.Trim());
        if (!string.IsNullOrWhiteSpace(department)) q = q.Where(x => x.Department == department.Trim());
        return await q.OrderBy(x => x.Name).Select(x => new Climate.Sensors.Application.Communities.CommunityResponse(
            x.Id, x.Name, x.Description, x.Latitude, x.Longitude, x.IsActive, x.CreatedAt, x.Municipality, x.Department, x.Country,
            dbContext.Sensors.Count(sensor => sensor.CommunityId == x.Id))).ToArrayAsync(token);
    }

    public async Task<IReadOnlyCollection<Sensor>> SearchSensorsAsync(Guid? communityId, SensorType? type, bool? isActive, string? code, string? search, CancellationToken token)
    {
        var q = dbContext.Sensors.AsNoTracking().AsQueryable();
        if (communityId.HasValue) q = q.Where(x => x.CommunityId == communityId.Value);
        if (type.HasValue) q = q.Where(x => x.Type == type.Value);
        if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(code)) { var normalized = code.Trim().ToUpperInvariant(); q = q.Where(x => x.NormalizedCode.Contains(normalized)); }
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); q = q.Where(x => x.Name.Contains(term) || x.Code.Contains(term)); }
        return await q.Include(x => x.Community).OrderBy(x => x.Code).ToArrayAsync(token);
    }
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
