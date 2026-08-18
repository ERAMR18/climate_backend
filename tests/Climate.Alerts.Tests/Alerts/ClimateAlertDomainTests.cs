using Climate.Alerts.Domain.Alerts;

namespace Climate.Alerts.Tests.Alerts;

public sealed class ClimateAlertDomainTests
{
    [Fact]
    public void UpdateAssessmentReactivatesResolvedAlert()
    {
        ClimateAlert alert=Create(); alert.Resolve(DateTimeOffset.UtcNow);
        DateTimeOffset updated=DateTimeOffset.UtcNow.AddMinutes(1); alert.UpdateAssessment(AlertLevel.Red,"Red","Escalated",8m,7m,updated);
        Assert.True(alert.IsActive); Assert.Null(alert.ResolvedAt); Assert.Equal(AlertLevel.Red,alert.Level); Assert.Equal(updated,alert.GeneratedAt);
    }

    [Fact]
    public void ResolveStoresExactTimestamp()
    {
        ClimateAlert alert=Create(); DateTimeOffset resolved=DateTimeOffset.UtcNow;
        alert.Resolve(resolved); Assert.False(alert.IsActive); Assert.Equal(resolved,alert.ResolvedAt);
    }

    private static ClimateAlert Create()=>ClimateAlert.Create(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),RiskType.Flood,AlertLevel.Yellow,"Flood","Threshold",4m,4m,DateTimeOffset.UtcNow);
}
