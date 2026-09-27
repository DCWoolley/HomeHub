using HomeHub.Infrastructure.Identity;
using HomeHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"], timeout: TimeSpan.FromSeconds(10));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<HomeHub.Application.Features.Meals.ManageMeals.IMeals, Meals.MealService>();
        services.AddScoped<HomeHub.Application.Features.Meals.PlanMeals.IMealPlanner, Meals.MealService>();
        services.AddScoped<HomeHub.Application.Features.Account.Register.IHomeRegistration, Homes.HomeService>();
        services.AddScoped<HomeHub.Application.Features.Homes.ManageHome.IHomeAccess, Homes.HomeService>();
        services.AddScoped<HomeHub.Application.Features.Homes.ManageMembers.IHomeMembers, Homes.HomeService>();

        return services;
    }

    public static async Task ApplyDatabaseMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }
}
