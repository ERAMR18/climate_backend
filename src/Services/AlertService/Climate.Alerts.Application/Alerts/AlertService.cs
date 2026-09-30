using Climate.Alerts.Application.Abstractions;
using Climate.Alerts.Application.Risk;
using Climate.Alerts.Domain.Alerts;
using Climate.Contracts.Monitoring;
using Climate.SharedKernel.Results;

namespace Climate.Alerts.Application.Alerts;

public sealed class AlertService(
    IAlertRepository repository,
    IRiskEvaluationService riskEvaluationService,
    IEventHistoryClient eventHistoryClient,
    TimeProvider timeProvider, ISensorActivityClient sensorActivity) : IAlertService
{
    public async Task<IReadOnlyCollection<AlertResponse>> ListAsync(
        AlertFilter filter,
        CancellationToken cancellationToken) =>
        (await repository.ListAsync(
            filter.RiskType,
            filter.AlertLevel,
            filter.SensorId,
            filter.CommunityId,
            filter.IsActive,
            cancellationToken, filter.From, filter.To, filter.Status)).Select(AlertResponse.FromEntity).ToArray();

    public async Task<Result<AlertResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        ClimateAlert? alert = await repository.GetByIdAsync(id, cancellationToken);
        return alert is null
            ? Result.Failure<AlertResponse>(AlertErrors.NotFound)
            : Result.Success(AlertResponse.FromEntity(alert));
    }

    public async Task<Result<IReadOnlyCollection<AlertResponse>>> EvaluateAsync(
        SensorReadingRecorded reading,
        CancellationToken cancellationToken)
    {
        if (!await sensorActivity.IsActiveAsync(reading.SensorId, cancellationToken))
            return Result.Success<IReadOnlyCollection<AlertResponse>>([]);
        IReadOnlyCollection<RiskAssessment> assessments = await riskEvaluationService.EvaluateAsync(
            reading.SensorType,
            reading.Value, cancellationToken);
        var affected = new List<AlertResponse>();

        foreach (RiskAssessment assessment in assessments)
        {
            ClimateAlert? active = await repository.GetActiveAsync(
                reading.SensorId,
                assessment.RiskType,
                cancellationToken);

            if (assessment.Level == AlertLevel.Green)
            {
                if (active is not null)
                {
                    active.Resolve(reading.RecordedAt);
                    affected.Add(AlertResponse.FromEntity(active));
                }

                continue;
            }

            if (active is null)
            {
                active = ClimateAlert.Create(
                    Guid.NewGuid(),
                    reading.SensorId,
                    reading.CommunityId,
                    assessment.RiskType,
                    assessment.Level,
                    assessment.Title,
                    assessment.Description,
                    reading.Value,
                    assessment.ThresholdValue,
                    reading.RecordedAt);
                await repository.AddAsync(active, cancellationToken);
            }
            else
            {
                active.UpdateAssessment(
                    assessment.Level,
                    assessment.Title,
                    assessment.Description,
                    reading.Value,
                    assessment.ThresholdValue,
                    reading.RecordedAt);
            }

            active.SetRule(assessment.RuleId, assessment.Title, assessment.MinimumValue, assessment.MaximumValue);
            affected.Add(AlertResponse.FromEntity(active));
        }

        if (affected.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
            foreach (AlertResponse response in affected)
            {
                ClimateAlert? alert = await repository.GetByIdAsync(response.Id, cancellationToken);
                if (alert is not null) await eventHistoryClient.RecordAsync(alert, cancellationToken);
            }
        }

        return Result.Success<IReadOnlyCollection<AlertResponse>>(affected);
    }

    public async Task<Result> TransitionAsync(Guid id, Guid userId, bool close, CancellationToken cancellationToken)
    {
        var alert = await repository.GetByIdAsync(id, cancellationToken);
        if (alert is null) return Result.Failure(AlertErrors.NotFound);
        bool changed = close ? alert.Close(userId, timeProvider.GetUtcNow()) : alert.Attend(userId, timeProvider.GetUtcNow());
        if (!changed) return Result.Failure(ApplicationError.Conflict("alert.invalid_transition", "Attend an active alert before closing it; a closed alert cannot be changed."));
        await repository.SaveChangesAsync(cancellationToken);
        await eventHistoryClient.RecordAsync(alert, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ResolveAsync(Guid id, CancellationToken cancellationToken)
    {
        ClimateAlert? alert = await repository.GetByIdAsync(id, cancellationToken);
        if (alert is null)
        {
            return Result.Failure(AlertErrors.NotFound);
        }

        alert.Resolve(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        await eventHistoryClient.RecordAsync(alert, cancellationToken);
        return Result.Success();
    }
}

public static class AlertErrors
{
    public static readonly ApplicationError NotFound = ApplicationError.NotFound(
        "alert.not_found",
        "The requested alert does not exist.");
}
