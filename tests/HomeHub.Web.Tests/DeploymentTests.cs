using System.Net;
using HomeHub.Web.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class DeploymentTests
{
    [Fact]
    public async Task HealthAndProtectedRoutesWorkWithUnavailableDatabase()
    {
        using var app = new UnavailableFactory();
        using var browser = app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("http://localhost") });
        Assert.Equal("Healthy", await browser.GetStringAsync("/health/live"));
        var ready = await browser.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Account/Login")).StatusCode);
    }

    [Fact]
    public void DataProtectionKeysSurviveHostRecreation()
    {
        var path = Path.Combine(Path.GetTempPath(), "HomeHub_RestartKeys_" + Guid.NewGuid().ToString("N"));
        try
        {
            string protectedValue;
            using (var first = new UnavailableFactory(path))
                protectedValue = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("cookie");
            using var second = new UnavailableFactory(path);
            Assert.Equal("cookie", second.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Unprotect(protectedValue));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    private sealed class UnavailableFactory(string? path = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Database:ApplyMigrations", "false");
            builder.UseSetting("Deployment:UseHttpsRedirection", "false");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=tcp:127.0.0.1,1;Database=Unavailable;User Id=test;Password=test;Connect Timeout=1;ConnectRetryCount=0");
            if (path is not null) builder.UseSetting("DataProtection:KeysPath", path);
        }
    }
}
