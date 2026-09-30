using Climate.Monitoring.Domain.Readings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Climate.Monitoring.Infrastructure.Persistence;
internal sealed class SimulationOverrideConfiguration : IEntityTypeConfiguration<SimulationOverride>
{
    public void Configure(EntityTypeBuilder<SimulationOverride> builder)
    {
        builder.ToTable("SimulationOverrides"); builder.HasKey(x => x.SensorId);
        builder.Property(x => x.Value).HasPrecision(18, 4);
    }
}
