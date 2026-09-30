using Climate.Alerts.Domain.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Climate.Alerts.Infrastructure.Persistence;
internal sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("AlertRules"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.MinimumValue).HasPrecision(18, 4);
        builder.Property(x => x.MaximumValue).HasPrecision(18, 4);
        builder.Property(x => x.AlertLevel).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.RiskType).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.SensorType, x.IsActive });
    }
}
