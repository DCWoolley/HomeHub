using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HomeHub.Web.Data;

public sealed class DatabaseHealthCheck(IDbContextFactory<ApplicationDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Database.CanConnectAsync(cancellationToken))
            return HealthCheckResult.Unhealthy("Database unavailable.");
        return (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any()
            ? HealthCheckResult.Unhealthy("Migrations pending.") : HealthCheckResult.Healthy();
    }
}
