using FluentValidation;

namespace Climate.Sensors.Application.Sensors;

public sealed class CreateSensorRequestValidator : AbstractValidator<CreateSensorRequest>
{
    public CreateSensorRequestValidator()
    {
        Include(new SensorFieldsValidator<CreateSensorRequest>());
        RuleFor(x => x.Location).MaximumLength(250);
        RuleFor(x => x.EnvironmentalType).MaximumLength(100);
        RuleFor(x => x.EnvironmentalType).NotEmpty().When(x => x.Type == Domain.Sensors.SensorType.Other);
    }
}

public sealed class UpdateSensorRequestValidator : AbstractValidator<UpdateSensorRequest>
{
    public UpdateSensorRequestValidator()
    {
        Include(new SensorFieldsValidator<UpdateSensorRequest>());
        RuleFor(x => x.Location).MaximumLength(250);
        RuleFor(x => x.EnvironmentalType).MaximumLength(100);
        RuleFor(x => x.EnvironmentalType).NotEmpty().When(x => x.Type == Domain.Sensors.SensorType.Other);
    }
}

internal sealed class SensorFieldsValidator<T> : AbstractValidator<T>
{
    public SensorFieldsValidator()
    {
        RuleFor(request => GetName(request)).NotEmpty().MaximumLength(120);
        RuleFor(request => GetCode(request)).NotEmpty().MaximumLength(50).Matches("^[a-zA-Z0-9_-]+$");
        RuleFor(request => GetDescription(request)).MaximumLength(500);
        RuleFor(request => GetType(request)).IsInEnum();
        RuleFor(request => GetUnit(request)).NotEmpty().MaximumLength(20);
        RuleFor(request => GetCommunityId(request)).NotEmpty();
        RuleFor(request => GetLatitude(request)).InclusiveBetween(-90, 90);
        RuleFor(request => GetLongitude(request)).InclusiveBetween(-180, 180);
    }

    private static string GetName(T request) => request switch
    {
        CreateSensorRequest create => create.Name,
        UpdateSensorRequest update => update.Name,
        _ => string.Empty
    };

    private static string GetCode(T request) => request switch
    {
        CreateSensorRequest create => create.Code,
        UpdateSensorRequest update => update.Code,
        _ => string.Empty
    };

    private static string? GetDescription(T request) => request switch
    {
        CreateSensorRequest create => create.Description,
        UpdateSensorRequest update => update.Description,
        _ => null
    };

    private static Domain.Sensors.SensorType GetType(T request) => request switch
    {
        CreateSensorRequest create => create.Type,
        UpdateSensorRequest update => update.Type,
        _ => default
    };

    private static string GetUnit(T request) => request switch
    {
        CreateSensorRequest create => create.Unit,
        UpdateSensorRequest update => update.Unit,
        _ => string.Empty
    };

    private static Guid GetCommunityId(T request) => request switch
    {
        CreateSensorRequest create => create.CommunityId,
        UpdateSensorRequest update => update.CommunityId,
        _ => Guid.Empty
    };

    private static decimal GetLatitude(T request) => request switch
    {
        CreateSensorRequest create => create.Latitude,
        UpdateSensorRequest update => update.Latitude,
        _ => default
    };

    private static decimal GetLongitude(T request) => request switch
    {
        CreateSensorRequest create => create.Longitude,
        UpdateSensorRequest update => update.Longitude,
        _ => default
    };
}
