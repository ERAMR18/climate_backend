using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Domain.Communities;
using Climate.Sensors.Domain.Sensors;
using Microsoft.EntityFrameworkCore;

namespace Climate.Sensors.Infrastructure.Persistence;

internal sealed class SensorCatalogRepository(SensorsDbContext dbContext, CommunityDirectory directory) : ISensorCatalogRepository
{
    public Task<int> CountSensorsAsync(Guid communityId, CancellationToken token) => dbContext.Sensors.CountAsync(x => x.CommunityId == communityId, token);
    public async Task<IReadOnlyCollection<Climate.Sensors.Application.Communities.CommunityResponse>> SearchCommunitiesAsync(string? search, bool? isActive, string? municipality, string? department, CancellationToken token)
    {
        if (directory.Enabled)
        {
            IReadOnlyCollection<Community> data = await directory.ListAsync(token);
            Dictionary<Guid, int> counts = await dbContext.Sensors.GroupBy(x => x.CommunityId).ToDictionaryAsync(x => x.Key, x => x.Count(), token);
            return data.Where(x => (string.IsNullOrWhiteSpace(search) || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                && (!isActive.HasValue || x.IsActive == isActive.Value)
                && (string.IsNullOrWhiteSpace(municipality) || x.Municipality == municipality.Trim())
                && (string.IsNullOrWhiteSpace(department) || x.Department == department.Trim()))
                .OrderBy(x => x.Name).Select(x => Climate.Sensors.Application.Communities.CommunityResponse.FromEntity(x)
                    with { SensorCount = counts.GetValueOrDefault(x.Id) }).ToArray();
        }
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
        IQueryable<Sensor> q = FilterSensors(communityId, type, isActive, code, search);
        Sensor[] sensors = await q.OrderBy(x => x.Code).ToArrayAsync(token);
        await HydrateAsync(sensors, token);
        return sensors;
    }
    public async Task<IReadOnlyCollection<Sensor>> SearchSensorsPageAsync(Guid? communityId, SensorType? type, bool? isActive, string? code, string? search, int page, int pageSize, CancellationToken token)
    {
        Sensor[] sensors = await FilterSensors(communityId, type, isActive, code, search).OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(token);
        await HydrateAsync(sensors, token);
        return sensors;
    }
    private IQueryable<Sensor> FilterSensors(Guid? communityId, SensorType? type, bool? isActive, string? code, string? search)
    {
        IQueryable<Sensor> q = dbContext.Sensors.AsNoTracking();
        if (communityId.HasValue) q = q.Where(x => x.CommunityId == communityId.Value);
        if (type.HasValue) q = q.Where(x => x.Type == type.Value);
        if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(code)) { var normalized = code.Trim().ToUpperInvariant(); q = q.Where(x => x.NormalizedCode.Contains(normalized)); }
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); q = q.Where(x => x.Name.Contains(term) || x.Code.Contains(term)); }
        return q;
    }
    public async Task<IReadOnlyCollection<Community>> ListCommunitiesAsync(CancellationToken cancellationToken) =>
        directory.Enabled ? await directory.ListAsync(cancellationToken)
            : await dbContext.Communities.AsNoTracking().OrderBy(community => community.Name).ToArrayAsync(cancellationToken);

    public Task<Community?> GetCommunityAsync(Guid id, CancellationToken cancellationToken) =>
        directory.Enabled ? directory.GetAsync(id, cancellationToken)
            : dbContext.Communities.SingleOrDefaultAsync(community => community.Id == id, cancellationToken);

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

    public async Task<IReadOnlyCollection<Sensor>> ListSensorsAsync(CancellationToken cancellationToken)
    {
        Sensor[] sensors = await dbContext.Sensors.AsNoTracking().OrderBy(sensor => sensor.Code).ToArrayAsync(cancellationToken);
        await HydrateAsync(sensors, cancellationToken);
        return sensors;
    }

    public async Task<Sensor?> GetSensorAsync(Guid id, CancellationToken cancellationToken)
    {
        Sensor? sensor = await dbContext.Sensors.SingleOrDefaultAsync(sensor => sensor.Id == id, cancellationToken);
        if (sensor is not null)
        {
            Community? community = await GetCommunityAsync(sensor.CommunityId, cancellationToken);
            sensor.AttachCommunitySnapshot(community ?? throw new HttpRequestException("Sensor community reference is invalid."));
        }
        return sensor;
    }

    private async Task HydrateAsync(Sensor[] sensors, CancellationToken token)
    {
        if (sensors.Length == 0) return;
        IReadOnlyCollection<Community> communities = await ListCommunitiesAsync(token);
        Dictionary<Guid, Community> byId = communities.ToDictionary(x => x.Id);
        foreach (Sensor sensor in sensors)
            sensor.AttachCommunitySnapshot(byId.GetValueOrDefault(sensor.CommunityId)
                ?? throw new HttpRequestException("Sensor community reference is invalid."));
    }

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
