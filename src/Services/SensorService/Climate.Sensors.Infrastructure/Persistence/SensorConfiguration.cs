using Climate.Sensors.Domain.Sensors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Climate.Sensors.Infrastructure.Persistence;

internal sealed class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> builder)
    {
        builder.ToTable("Sensors");
        builder.Property(x => x.Location).HasMaxLength(250);
        builder.Property(x => x.EnvironmentalType).HasMaxLength(100);
        builder.HasKey(sensor => sensor.Id);
        builder.Property(sensor => sensor.Name).HasMaxLength(120).IsRequired();
        builder.Property(sensor => sensor.Code).HasMaxLength(50).IsRequired();
        builder.Property(sensor => sensor.NormalizedCode).HasMaxLength(50).IsRequired();
        builder.Property(sensor => sensor.Description).HasMaxLength(500);
        builder.Property(sensor => sensor.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(sensor => sensor.Unit).HasMaxLength(20).IsRequired();
        builder.Property(sensor => sensor.Latitude).HasPrecision(9, 6);
        builder.Property(sensor => sensor.Longitude).HasPrecision(9, 6);
        builder.Property(sensor => sensor.CreatedAt).HasPrecision(0);
        builder.Property(sensor => sensor.UpdatedAt).HasPrecision(0);

        builder.HasIndex(sensor => sensor.NormalizedCode).IsUnique();
        builder.HasIndex(sensor => new { sensor.CommunityId, sensor.IsActive });
        builder.HasIndex(sensor => new { sensor.Type, sensor.IsActive });

        builder.HasOne(sensor => sensor.Community)
            .WithMany()
            .HasForeignKey(sensor => sensor.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
