using Climate.Sensors.Domain.Communities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Climate.Sensors.Infrastructure.Persistence;

internal sealed class CommunityConfiguration : IEntityTypeConfiguration<Community>
{
    public void Configure(EntityTypeBuilder<Community> builder)
    {
        builder.ToTable("Communities");
        builder.Property(x => x.Municipality).HasMaxLength(120);
        builder.Property(x => x.Department).HasMaxLength(120);
        builder.Property(x => x.Country).HasMaxLength(120);
        builder.HasKey(community => community.Id);
        builder.Property(community => community.Name).HasMaxLength(120).IsRequired();
        builder.Property(community => community.Description).HasMaxLength(500);
        builder.Property(community => community.Latitude).HasPrecision(9, 6);
        builder.Property(community => community.Longitude).HasPrecision(9, 6);
        builder.Property(community => community.CreatedAt).HasPrecision(0);
        builder.HasIndex(community => community.Name).IsUnique();
        builder.HasIndex(community => community.IsActive);
    }
}
