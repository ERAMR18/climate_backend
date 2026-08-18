using Climate.Alerts.Application.Risk;
using Climate.Alerts.Domain.Alerts;
using Climate.Contracts.Sensors;
using Moq;

namespace Climate.Alerts.Tests.Risk;

public sealed class RiskEvaluationServiceTests
{
    [Theory]
    [InlineData(3, AlertLevel.Green)]
    [InlineData(4, AlertLevel.Yellow)]
    [InlineData(6, AlertLevel.Orange)]
    [InlineData(7, AlertLevel.Red)]
    public void IncreasingRuleClassifiesLevel(decimal value, AlertLevel expected)
    {
        RiskRule rule = CreateRule(ThresholdDirection.Increasing, 4m, 5.5m, 7m);
        var provider = new Mock<IRiskRuleProvider>();
        provider.Setup(value => value.GetRules(SensorType.WaterLevel)).Returns([rule]);

        RiskAssessment assessment = new RiskEvaluationService(provider.Object)
            .Evaluate(SensorType.WaterLevel, value)
            .Single();

        Assert.Equal(expected, assessment.Level);
    }

    [Theory]
    [InlineData(8, AlertLevel.Green)]
    [InlineData(5, AlertLevel.Yellow)]
    [InlineData(2, AlertLevel.Orange)]
    [InlineData(0, AlertLevel.Red)]
    public void DecreasingRuleClassifiesLevel(decimal value, AlertLevel expected)
    {
        RiskRule rule = CreateRule(ThresholdDirection.Decreasing, 5m, 2m, 0m);
        var provider = new Mock<IRiskRuleProvider>();
        provider.Setup(item => item.GetRules(SensorType.Temperature)).Returns([rule]);

        RiskAssessment assessment = new RiskEvaluationService(provider.Object)
            .Evaluate(SensorType.Temperature, value)
            .Single();

        Assert.Equal(expected, assessment.Level);
    }

    [Fact]
    public void TemperatureCanEvaluateMultipleRiskStrategies()
    {
        RiskRule frost = CreateRule(ThresholdDirection.Decreasing, 5m, 2m, 0m) with
        {
            SensorType = SensorType.Temperature,
            RiskType = RiskType.Frost
        };
        RiskRule fire = frost with
        {
            RiskType = RiskType.ForestFire,
            Direction = ThresholdDirection.Increasing,
            Yellow = 32m,
            Orange = 38m,
            Red = 43m
        };
        var provider = new Mock<IRiskRuleProvider>();
        provider.Setup(item => item.GetRules(SensorType.Temperature)).Returns([frost, fire]);

        IReadOnlyCollection<RiskAssessment> assessments = new RiskEvaluationService(provider.Object)
            .Evaluate(SensorType.Temperature, 40m);

        Assert.Equal(2, assessments.Count);
        Assert.Contains(assessments, item => item.RiskType == RiskType.Frost && item.Level == AlertLevel.Green);
        Assert.Contains(assessments, item => item.RiskType == RiskType.ForestFire && item.Level == AlertLevel.Orange);
    }

    private static RiskRule CreateRule(
        ThresholdDirection direction,
        decimal yellow,
        decimal orange,
        decimal red) =>
        new(
            SensorType.WaterLevel,
            RiskType.Flood,
            direction,
            yellow,
            orange,
            red,
            "Risk title",
            "Configured demonstration threshold exceeded.");
}
