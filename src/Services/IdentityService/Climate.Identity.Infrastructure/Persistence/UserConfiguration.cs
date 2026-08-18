using Climate.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Climate.Identity.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username).HasMaxLength(50).IsRequired();
        builder.Property(user => user.NormalizedUsername).HasMaxLength(50).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(254).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(254).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(user => user.Role).HasMaxLength(32).IsRequired();
        builder.Property(user => user.CreatedAt).HasPrecision(0);
        builder.Property(user => user.UpdatedAt).HasPrecision(0);

        builder.HasIndex(user => user.NormalizedUsername).IsUnique();
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
        builder.HasIndex(user => new { user.IsActive, user.Role });
    }
}
