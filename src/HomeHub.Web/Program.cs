using HomeHub.Web.Components;
using HomeHub.Web.Features.Account;
using HomeHub.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace HomeHub.Web
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// Add services to the container.
			builder.Services.AddRazorComponents()
				.AddInteractiveServerComponents();

			builder.Services.AddInfrastructure(builder.Configuration);
			builder.Services.AddAccountFeature();
			builder.Services.AddDatabaseDeveloperPageExceptionFilter();

			var keysPath = builder.Configuration["DataProtection:KeysPath"];
			if (!string.IsNullOrWhiteSpace(keysPath))
			{
				builder.Services.AddDataProtection()
					.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
			}

			var app = builder.Build();
			if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
			{
				await app.Services.ApplyDatabaseMigrationsAsync(app.Lifetime.ApplicationStopping);
			}

			var useHttpsRedirection = app.Configuration.GetValue("Deployment:UseHttpsRedirection", true);

			// Configure the HTTP request pipeline.
			if (app.Environment.IsDevelopment())
			{
				app.UseMigrationsEndPoint();
			}
			else
			{
				app.UseExceptionHandler("/Error");
				if (useHttpsRedirection)
				{
					app.UseHsts();
				}
			}

			app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
			if (useHttpsRedirection)
			{
				app.UseHttpsRedirection();
			}

			app.UseAntiforgery();

			app.MapStaticAssets();
			app.MapRazorComponents<App>()
				.AddInteractiveServerRenderMode();

			// Add additional endpoints required by the Identity /Account Razor components.
			app.MapAccountEndpoints();
			app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
			app.MapHealthChecks("/health/ready", new HealthCheckOptions
			{
				Predicate = registration => registration.Tags.Contains("ready")
			});

			await app.RunAsync();
		}
	}
}
