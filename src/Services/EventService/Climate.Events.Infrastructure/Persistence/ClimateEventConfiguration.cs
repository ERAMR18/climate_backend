using Climate.Events.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Climate.Events.Infrastructure.Persistence;

internal sealed class ClimateEventConfiguration : IEntityTypeConfiguration<ClimateEvent>
{
    public void Configure(EntityTypeBuilder<ClimateEvent> builder)
    {
        builder.ToTable("ClimateEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RiskType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.AlertLevel).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => x.AlertId).IsUnique();
        builder.HasIndex(x => new { x.RiskType, x.AlertLevel, x.OccurredAt });
        builder.HasIndex(x => new { x.SensorId, x.OccurredAt });
        builder.HasIndex(x => new { x.CommunityId, x.OccurredAt });
    }
}
