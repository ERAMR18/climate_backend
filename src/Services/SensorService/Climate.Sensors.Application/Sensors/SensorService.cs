using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Application.Common;
using Climate.Sensors.Application.Communities;
using Climate.Sensors.Domain.Communities;
using Climate.Sensors.Domain.Sensors;
using Climate.SharedKernel.Results;
using FluentValidation;

namespace Climate.Sensors.Application.Sensors;

public interface ISensorService
{
    Task<IReadOnlyCollection<SensorResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<Result<SensorResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<SensorResponse>> CreateAsync(CreateSensorRequest request, CancellationToken cancellationToken);
    Task<Result<SensorResponse>> UpdateAsync(Guid id, UpdateSensorRequest request, CancellationToken cancellationToken);
    Task<Result> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}

public sealed class SensorService(
    ISensorCatalogRepository repository,
    IValidator<CreateSensorRequest> createValidator,
    IValidator<UpdateSensorRequest> updateValidator,
    TimeProvider timeProvider) : ISensorService
{
    public async Task<IReadOnlyCollection<SensorResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.ListSensorsAsync(cancellationToken)).Select(sensor => SensorResponse.FromEntity(sensor)).ToArray();

    public async Task<Result<SensorResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Sensor? sensor = await repository.GetSensorAsync(id, cancellationToken);
        return sensor is null
            ? Result.Failure<SensorResponse>(SensorErrors.NotFound)
            : Result.Success(SensorResponse.FromEntity(sensor));
    }

    public async Task<Result<SensorResponse>> CreateAsync(
        CreateSensorRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SensorResponse>(ValidationError.From(validation));
        }

        if (await repository.SensorCodeExistsAsync(request.Code, null, cancellationToken))
        {
            return Result.Failure<SensorResponse>(SensorErrors.CodeConflict);
        }

        Result<Community> communityResult = await GetActiveCommunityAsync(request.CommunityId, cancellationToken);
        if (communityResult.IsFailure)
        {
            return Result.Failure<SensorResponse>(communityResult.Error);
        }

        Sensor sensor = Sensor.Create(
            Guid.NewGuid(),
            request.Name,
            request.Code,
            request.Description,
            request.Type,
            request.Unit,
            request.CommunityId,
            request.Latitude,
            request.Longitude,
            timeProvider.GetUtcNow());
        await repository.AddSensorAsync(sensor, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(SensorResponse.FromEntity(sensor, communityResult.Value.Name));
    }

    public async Task<Result<SensorResponse>> UpdateAsync(
        Guid id,
        UpdateSensorRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SensorResponse>(ValidationError.From(validation));
        }

        Sensor? sensor = await repository.GetSensorAsync(id, cancellationToken);
        if (sensor is null)
        {
            return Result.Failure<SensorResponse>(SensorErrors.NotFound);
        }

        if (await repository.SensorCodeExistsAsync(request.Code, id, cancellationToken))
        {
            return Result.Failure<SensorResponse>(SensorErrors.CodeConflict);
        }

        Result<Community> communityResult = await GetActiveCommunityAsync(request.CommunityId, cancellationToken);
        if (communityResult.IsFailure)
        {
            return Result.Failure<SensorResponse>(communityResult.Error);
        }

        sensor.Update(
            request.Name,
            request.Code,
            request.Description,
            request.Type,
            request.Unit,
            request.CommunityId,
            request.Latitude,
            request.Longitude,
            timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(SensorResponse.FromEntity(sensor, communityResult.Value.Name));
    }

    public async Task<Result> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        Sensor? sensor = await repository.GetSensorAsync(id, cancellationToken);
        if (sensor is null)
        {
            return Result.Failure(SensorErrors.NotFound);
        }

        if (isActive)
        {
            Community? community = await repository.GetCommunityAsync(sensor.CommunityId, cancellationToken);
            if (community is null || !community.IsActive)
            {
                return Result.Failure(CommunityErrors.Inactive);
            }
        }

        sensor.SetStatus(isActive, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<Community>> GetActiveCommunityAsync(
        Guid communityId,
        CancellationToken cancellationToken)
    {
        Community? community = await repository.GetCommunityAsync(communityId, cancellationToken);
        if (community is null)
        {
            return Result.Failure<Community>(CommunityErrors.NotFound);
        }

        return community.IsActive
            ? Result.Success(community)
            : Result.Failure<Community>(CommunityErrors.Inactive);
    }
}

public static class SensorErrors
{
    public static readonly ApplicationError NotFound = ApplicationError.NotFound(
        "sensor.not_found",
        "The requested sensor does not exist.");

    public static readonly ApplicationError CodeConflict = ApplicationError.Conflict(
        "sensor.code_conflict",
        "A sensor with the same code already exists.");
}
