using Climate.Alerts.Domain.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Climate.Alerts.Infrastructure.Persistence;

internal sealed class AlertConfiguration : IEntityTypeConfiguration<ClimateAlert>
{
    public void Configure(EntityTypeBuilder<ClimateAlert> builder)
    {
        builder.ToTable("Alerts");
        builder.HasKey(alert => alert.Id);
        builder.Property(alert => alert.AlertType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(alert => alert.Level).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(alert => alert.Title).HasMaxLength(200).IsRequired();
        builder.Property(alert => alert.Description).HasMaxLength(1000).IsRequired();
        builder.Property(alert => alert.SensorValue).HasPrecision(18, 4);
        builder.Property(alert => alert.ThresholdValue).HasPrecision(18, 4);
        builder.Property(alert => alert.GeneratedAt).HasPrecision(3);
        builder.Property(alert => alert.ResolvedAt).HasPrecision(3);
        builder.HasIndex(alert => new { alert.SensorId, alert.AlertType, alert.IsActive });
        builder.HasIndex(alert => new { alert.CommunityId, alert.IsActive });
        builder.HasIndex(alert => new { alert.Level, alert.IsActive });
        builder.HasIndex(alert => alert.GeneratedAt);
    }
}
