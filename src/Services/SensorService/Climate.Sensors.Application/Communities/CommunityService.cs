using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Application.Common;
using Climate.Sensors.Domain.Communities;
using Climate.SharedKernel.Results;
using FluentValidation;

namespace Climate.Sensors.Application.Communities;

public interface ICommunityService
{
    Task<IReadOnlyCollection<CommunityResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<Result<CommunityResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<CommunityResponse>> CreateAsync(CreateCommunityRequest request, CancellationToken cancellationToken);
    Task<Result<CommunityResponse>> UpdateAsync(Guid id, UpdateCommunityRequest request, CancellationToken cancellationToken);
}

public sealed class CommunityService(
    ISensorCatalogRepository repository,
    IValidator<CreateCommunityRequest> createValidator,
    IValidator<UpdateCommunityRequest> updateValidator,
    TimeProvider timeProvider) : ICommunityService
{
    public async Task<IReadOnlyCollection<CommunityResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.ListCommunitiesAsync(cancellationToken)).Select(CommunityResponse.FromEntity).ToArray();

    public async Task<Result<CommunityResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Community? community = await repository.GetCommunityAsync(id, cancellationToken);
        return community is null
            ? Result.Failure<CommunityResponse>(CommunityErrors.NotFound)
            : Result.Success(CommunityResponse.FromEntity(community) with { SensorCount = await repository.CountSensorsAsync(id, cancellationToken) });
    }

    public async Task<Result<CommunityResponse>> CreateAsync(
        CreateCommunityRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CommunityResponse>(ValidationError.From(validation));
        }

        if (await repository.CommunityNameExistsAsync(request.Name, null, cancellationToken))
        {
            return Result.Failure<CommunityResponse>(CommunityErrors.NameConflict);
        }

        Community community = Community.Create(
            Guid.NewGuid(),
            request.Name,
            request.Description,
            request.Latitude,
            request.Longitude,
            timeProvider.GetUtcNow());
        community.SetGeography(request.Municipality, request.Department, request.Country);
        await repository.AddCommunityAsync(community, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(CommunityResponse.FromEntity(community));
    }

    public async Task<Result<CommunityResponse>> UpdateAsync(
        Guid id,
        UpdateCommunityRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CommunityResponse>(ValidationError.From(validation));
        }

        Community? community = await repository.GetCommunityAsync(id, cancellationToken);
        if (community is null)
        {
            return Result.Failure<CommunityResponse>(CommunityErrors.NotFound);
        }

        if (await repository.CommunityNameExistsAsync(request.Name, id, cancellationToken))
        {
            return Result.Failure<CommunityResponse>(CommunityErrors.NameConflict);
        }

        community.Update(request.Name, request.Description, request.Latitude, request.Longitude, request.IsActive);
        community.SetGeography(request.Municipality, request.Department, request.Country);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(CommunityResponse.FromEntity(community) with { SensorCount = await repository.CountSensorsAsync(id, cancellationToken) });
    }
}

public static class CommunityErrors
{
    public static readonly ApplicationError NotFound = ApplicationError.NotFound(
        "community.not_found",
        "The requested community does not exist.");

    public static readonly ApplicationError NameConflict = ApplicationError.Conflict(
        "community.name_conflict",
        "A community with the same name already exists.");

    public static readonly ApplicationError Inactive = ApplicationError.Validation(
        "community.inactive",
        "An active sensor must belong to an active community.");
}
