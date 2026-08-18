using Climate.Contracts.Identity;
using Climate.Identity.Application.Abstractions;
using Climate.Identity.Domain.Users;
using Climate.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Climate.Identity.Infrastructure.Seeding;

public static class IdentityDatabaseInitializer
{
    private static readonly Action<ILogger, Exception?> LogAdminSeedSkipped = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1001, nameof(LogAdminSeedSkipped)),
        "Administrator seed was skipped because AdminSeed configuration is incomplete.");

    public static async Task InitializeIdentityDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        AdminSeedOptions options = scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.Username) ||
            string.IsNullOrWhiteSpace(options.Email) ||
            string.IsNullOrWhiteSpace(options.Password))
        {
            LogAdminSeedSkipped(
                scope.ServiceProvider.GetRequiredService<ILogger<IdentityDbContext>>(),
                null);
            return;
        }

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        IPasswordService passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        DateTimeOffset now = timeProvider.GetUtcNow();
        User admin = User.Create(
            Guid.NewGuid(),
            options.Username,
            options.Email,
            string.Empty,
            SystemRoles.Administrator,
            now);

        admin.SetPasswordHash(passwordService.Hash(admin, options.Password), now);
        await dbContext.Users.AddAsync(admin, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
