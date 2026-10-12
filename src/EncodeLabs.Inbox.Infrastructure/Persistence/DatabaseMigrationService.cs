using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EncodeLabs.Inbox.Infrastructure.Persistence;

internal sealed class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    private const int MaxAttempts = 10;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("PostgreSQL migrations applied successfully");
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (attempt < MaxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "PostgreSQL migration attempt {Attempt}/{MaxAttempts} failed",
                    attempt,
                    MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }

        using var finalScope = scopeFactory.CreateScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await finalDbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
