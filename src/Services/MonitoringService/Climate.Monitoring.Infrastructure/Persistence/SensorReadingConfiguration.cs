using Climate.Monitoring.Domain.Readings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Climate.Monitoring.Infrastructure.Persistence;

internal sealed class SensorReadingConfiguration : IEntityTypeConfiguration<SensorReading>
{
    public void Configure(EntityTypeBuilder<SensorReading> builder)
    {
        builder.ToTable("SensorReadings");
        builder.HasKey(reading => reading.Id);
        builder.Property(reading => reading.SensorType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(reading => reading.Value).HasPrecision(18, 4);
        builder.Property(reading => reading.Unit).HasMaxLength(20).IsRequired();
        builder.Property(reading => reading.RecordedAt).HasPrecision(3);
        builder.HasIndex(reading => new { reading.SensorId, reading.RecordedAt });
        builder.HasIndex(reading => new { reading.CommunityId, reading.RecordedAt });
        builder.HasIndex(reading => reading.RecordedAt);
    }
}
