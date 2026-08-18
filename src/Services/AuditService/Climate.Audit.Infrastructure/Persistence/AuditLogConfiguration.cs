using Climate.Audit.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Climate.Audit.Infrastructure.Persistence;
internal sealed class AuditLogConfiguration:IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs"); b.HasKey(x=>x.Id);
        b.Property(x=>x.UserName).HasMaxLength(100).IsRequired(); b.Property(x=>x.Action).HasMaxLength(64).IsRequired();
        b.Property(x=>x.Resource).HasMaxLength(100).IsRequired(); b.Property(x=>x.ResourceId).HasMaxLength(100);
        b.Property(x=>x.Description).HasMaxLength(1000).IsRequired(); b.Property(x=>x.IpAddress).HasMaxLength(64);
        b.HasIndex(x=>x.Timestamp); b.HasIndex(x=>new{x.UserId,x.Timestamp}); b.HasIndex(x=>new{x.Action,x.Timestamp});
    }
}
