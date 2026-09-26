using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HomeHub.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(ApplicationDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!await database.Database.CanConnectAsync(cancellationToken))
        {
            return HealthCheckResult.Unhealthy("The database is unavailable.");
        }

        var pendingMigrations = await database.Database.GetPendingMigrationsAsync(cancellationToken);
        return pendingMigrations.Any()
            ? HealthCheckResult.Unhealthy("The database has pending migrations.")
            : HealthCheckResult.Healthy();
    }
}
