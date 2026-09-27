using System.Threading.RateLimiting;

namespace HomeHub.Web.Features.Homes.JoinHome;

public static class JoinAttemptRateLimiting
{
    public static IServiceCollection AddJoinAttemptRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var path = context.Request.Path.Value?.TrimEnd('/');
                var validatesInvitation = HttpMethods.IsPost(context.Request.Method) &&
                    (string.Equals(path, "/Home", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(path, "/Account/Register", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(path, "/Account/ExternalLogin", StringComparison.OrdinalIgnoreCase));
                return validatesInvitation
                    ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    })
                    : RateLimitPartition.GetNoLimiter("unrestricted");
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsync("Too many joining attempts. Wait one minute and try again.", cancellationToken);
            };
        });
        return services;
    }
}
