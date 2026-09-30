using Climate.Alerts.Api.Controllers;
using Climate.Alerts.Domain.Alerts;
using Climate.Alerts.Infrastructure.Persistence;
using Climate.Alerts.Infrastructure.Risk;
using Climate.Contracts.Sensors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Climate.Alerts.Tests.Alerts;

public sealed class RulesWorkflowTests
{
    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(15, false)]
    [InlineData(20, false)]
    [InlineData(21, true)]
    public void AcceptableIntervalIncludesBothBoundaries(int value, bool violated)
    {
        var rule = new AlertRule { MinimumValue = 10m, MaximumValue = 20m };
        Assert.Equal(violated, rule.IsViolated(value));
    }

    [Fact]
    public async Task RuleChangesAndDeactivationAffectNextEvaluation()
    {
        await using var db = new AlertsDbContext(new DbContextOptionsBuilder<AlertsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var api = new AlertRulesController(db);
        var request = new AlertRuleRequest("Smoke warning", SensorType.Smoke, null, 50m,
            AlertLevel.Yellow, RiskType.ForestFire, "Smoke concentration high");
        var created = Assert.IsType<CreatedAtActionResult>((await api.Create(request, default)).Result);
        var rule = Assert.IsType<AlertRuleResponse>(created.Value);
        var engine = new DatabaseRiskEvaluationService(db);
        var assessment = Assert.Single(await engine.EvaluateAsync(SensorType.Smoke, 60m, default));
        Assert.Equal(rule.Id, assessment.RuleId);
        Assert.Equal(50m, assessment.MaximumValue);
        Assert.Empty(await engine.EvaluateAsync(SensorType.Humidity, 60m, default));
        await api.Update(rule.Id, request with { MaximumValue = 70m }, default);
        Assert.Empty(await engine.EvaluateAsync(SensorType.Smoke, 60m, default));
        await api.Deactivate(rule.Id, default);
        Assert.Empty(await engine.EvaluateAsync(SensorType.Smoke, 99m, default));
        await api.Activate(rule.Id, default);
        Assert.Single(await engine.EvaluateAsync(SensorType.Smoke, 99m, default));
    }

    [Fact]
    public void MostSevereViolatedRuleWinsPerPhenomenon()
    {
        AlertRule[] rules = [
            new() { Id = Guid.NewGuid(), MaximumValue = 10m, AlertLevel = AlertLevel.Yellow, RiskType = RiskType.Storm },
            new() { Id = Guid.NewGuid(), MaximumValue = 20m, AlertLevel = AlertLevel.Red, RiskType = RiskType.Storm },
            new() { MaximumValue = 10m, AlertLevel = AlertLevel.Red, RiskType = RiskType.Flood, IsActive = false },
            new() { MaximumValue = 10m, AlertLevel = AlertLevel.Green, RiskType = RiskType.Frost }];
        var assessment = Assert.Single(DatabaseRiskEvaluationService.Assess(rules, 30m));
        Assert.Equal(rules[1].Id, assessment.RuleId);
    }

    [Fact]
    public void WorkflowRequiresAttendBeforeCloseAndPreservesActorAcrossAssessments()
    {
        var alert = ClimateAlert.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RiskType.Flood,
            AlertLevel.Yellow, "Flood", "Rising", 4m, 3m, DateTimeOffset.UtcNow);
        Guid operatorId = Guid.NewGuid(), closerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.False(alert.Close(closerId, now));
        Assert.False(alert.Attend(Guid.Empty, now));
        Assert.True(alert.Attend(operatorId, now));
        Assert.False(alert.Attend(operatorId, now));
        alert.UpdateAssessment(AlertLevel.Red, "Flood", "Rising", 10m, 3m, now);
        Assert.Equal(AlertStatus.Attended, alert.Status);
        Assert.Equal(operatorId, alert.AttendedByUserId);
        Assert.True(alert.Close(closerId, now.AddMinutes(1)));
        Assert.False(alert.IsActive);
        Assert.Equal(closerId, alert.ClosedByUserId);
        Assert.Equal(now.AddMinutes(1), alert.ClosedAt);
        Assert.False(alert.Close(closerId, now));
        Assert.False(alert.Attend(operatorId, now));
    }

    [Fact]
    public void RulesRejectInvalidIntervalsAndOnlyAdministratorsCanWrite()
    {
        var request = new AlertRuleRequest("Rule", SensorType.Temperature, 20m, 10m, AlertLevel.Red, RiskType.Frost, "Message");
        Assert.False(request.IsValid());
        Assert.False((request with { MinimumValue = null, MaximumValue = null }).IsValid());
        Assert.False((request with { MinimumValue = 0, SensorType = (SensorType)999 }).IsValid());
        foreach (string method in new[] { "Create", "Update", "Activate", "Deactivate" })
            Assert.Contains(typeof(AlertRulesController).GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), true),
                x => ((AuthorizeAttribute)x).Roles == "Administrator");
    }
}
