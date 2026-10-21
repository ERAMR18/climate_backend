using Climate.Contracts.Audit;
using Microsoft.EntityFrameworkCore;

namespace Climate.Communities.Api.Persistence;

public sealed class Community
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public string? Municipality { get; set; }
    public string? Department { get; set; }
    public string? Country { get; set; }
}

public sealed class CommunityDbContext(DbContextOptions<CommunityDbContext> options) : DbContext(options)
{
    public DbSet<Community> Communities => Set<Community>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddAuditOutbox();
        modelBuilder.Entity<Community>(entity =>
        {
            entity.ToTable("Communities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Latitude).HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasPrecision(9, 6);
            entity.Property(x => x.Municipality).HasMaxLength(120);
            entity.Property(x => x.Department).HasMaxLength(120);
            entity.Property(x => x.Country).HasMaxLength(120);
        });
    }
    public async Task InitializeAsync(CancellationToken token)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await Database.BeginTransactionAsync(token);
        await Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(31003)", token);
        using Stream stream = typeof(CommunityDbContext).Assembly.GetManifestResourceStream("Climate.Communities.Api.Persistence.001_initial.sql")
            ?? throw new InvalidOperationException("Community schema resource is missing.");
        using StreamReader reader = new(stream);
        string script = await reader.ReadToEndAsync(token);
        await Database.ExecuteSqlRawAsync(script, token);
        await transaction.CommitAsync(token);
    }
}
