using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HomeHub.Web.Components;
using HomeHub.Web.Modules.Account;
using HomeHub.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazorBlueprintComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString), ServiceLifetime.Scoped);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddScoped<HomeHub.Web.Modules.Account.Register.RegisterAccount>();
builder.Services.AddScoped<HomeHub.Web.Modules.Households.ManageInvitations.ManageInvitations>();
builder.Services.AddScoped<HomeHub.Web.Modules.Households.ManageAccountMembers.ManageAccountMembers>();
builder.Services.AddScoped<HomeHub.Web.Modules.Households.ManageProfiles.ManageProfiles>();
builder.Services.AddScoped<HomeHub.Web.Modules.Households.TransferOwnership.TransferOwnership>();
builder.Services.AddScoped<HomeHub.Web.Modules.Households.LeaveHousehold.LeaveHousehold>();
builder.Services.AddScoped<HomeHub.Web.Modules.Account.Manage.DeletePersonalData.DeleteAccount>();
builder.Services.AddScoped<HomeHub.Web.Modules.Households.GetHousehold.GetHousehold>();
builder.Services.AddScoped<HomeHub.Web.Modules.Households.Onboarding.Onboarding>();
builder.Services.AddScoped<HomeHub.Web.Modules.MealPlanner.ManageMeals.ManageMeals>();
builder.Services.AddScoped<HomeHub.Web.Modules.MealPlanner.ManageIngredients.ManageIngredients>();
builder.Services.AddScoped<HomeHub.Web.Modules.MealPlanner.ManageIngredientUnits.ManageIngredientUnits>();
builder.Services.AddScoped<HomeHub.Web.Modules.MealPlanner.ManageRecipe.ManageRecipe>();
builder.Services.AddScoped<HomeHub.Web.Modules.MealPlanner.PlanWeek.PlanWeek>();
builder.Services.AddScoped<HomeHub.Web.Modules.MealPlanner.ManagePlannerSettings.ManagePlannerSettings>();
builder.Services.AddScoped<HomeHub.Web.Modules.MealPlanner.CreateMealFromPlanner.CreateMealFromPlanner>();
builder.Services.AddScoped<HomeHub.Web.Modules.HouseholdItems.ManageCatalogue.ManageCatalogue>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection().SetApplicationName("HomeHub").PersistKeysToFileSystem(new DirectoryInfo(keysPath));

var app = builder.Build();
if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
}
var useHttps = app.Configuration.GetValue("Deployment:UseHttpsRedirection", true);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    if (useHttps) app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (useHttps) app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var householdRoute = path.StartsWithSegments("/Household") && !path.StartsWithSegments("/Household/Onboarding");
    if (context.User.Identity?.IsAuthenticated == true && (householdRoute || path.StartsWithSegments("/Dashboard") || path.StartsWithSegments("/Meals") || path.StartsWithSegments("/Ingredients") || path.StartsWithSegments("/HouseholdItems") || path.StartsWithSegments("/MealPlanner")))
    {
        var current = context.RequestServices.GetRequiredService<HomeHub.Web.Modules.Households.GetHousehold.GetHousehold>();
        if (await current.RunAsync(context.User, context.RequestAborted) is null)
        {
            context.Response.Redirect("/Household/Onboarding");
            return;
        }
    }
    await next(context);
});
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("ready") });
app.Run();

public partial class Program { }
