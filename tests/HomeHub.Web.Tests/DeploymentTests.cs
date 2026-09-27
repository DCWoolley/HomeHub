using System.Net;
using HomeHub.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class DeploymentTests
{
    [Fact]
    public async Task LivenessSucceedsWhenDatabaseIsUnavailable()
    {
        using var factory = new DeploymentFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadinessFailsWhenDatabaseIsUnavailableWithoutExposingDetails()
    {
        using var factory = new DeploymentFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForgotPassword")]
    [InlineData("/Account/ResendEmailConfirmation")]
    [InlineData("/Account/ResetPassword?code=dGVzdA")]
    public async Task LanRoutesRenderWithoutRedirectingToHttps(string path)
    {
        using var factory = new DeploymentFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("id=\"app-sidebar\"", await response.Content.ReadAsStringAsync());
        if (path.StartsWith("/Account/"))
        {
            Assert.Contains("__RequestVerificationToken", await response.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData("/Account/Manage")]
    [InlineData("/Account/Manage/Email")]
    [InlineData("/Account/Manage/Passkeys")]
    [InlineData("/Account/Manage/TwoFactorAuthentication")]
    [InlineData("/Account/Manage/DeletePersonalData")]
    [InlineData("/Home")]
    [InlineData("/Home/Configure")]
    [InlineData("/Home/Members")]
    [InlineData("/Meals")]
    [InlineData("/Meals/New")]
    [InlineData("/Meals/Edit/00000000-0000-0000-0000-000000000001")]
    [InlineData("/MealPlanner")]
    [InlineData("/MealPlanner/Entry/00000000-0000-0000-0000-000000000001")]
    [InlineData("/Home/Members/New")]
    [InlineData("/Home/Members/Edit/00000000-0000-0000-0000-000000000001")]
    [InlineData("/Home/Members/Remove/00000000-0000-0000-0000-000000000001")]
    public async Task AccountManagementStillRequiresAuthentication(string path)
    {
        using var factory = new DeploymentFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public void PersistedKeysCanDecryptDataAfterHostRecreation()
    {
        var keysPath = Path.Combine(Path.GetTempPath(), "homehub-tests", Guid.NewGuid().ToString("N"));
        try
        {
            string protectedValue;
            using (var firstHost = new DeploymentFactory(keysPath))
            {
                var protector = firstHost.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("deployment-test");
                protectedValue = protector.Protect("persistent-authentication-data");
            }

            Assert.NotEmpty(Directory.GetFiles(keysPath, "key-*.xml"));
            using var secondHost = new DeploymentFactory(keysPath);
            var secondProtector = secondHost.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("deployment-test");
            Assert.Equal("persistent-authentication-data", secondProtector.Unprotect(protectedValue));
        }
        finally
        {
            if (Directory.Exists(keysPath))
            {
                Directory.Delete(keysPath, recursive: true);
            }
        }
    }

    private static HttpClient CreateClient(DeploymentFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("http://localhost"),
        AllowAutoRedirect = false
    });

    private sealed class DeploymentFactory(string? keysPath = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=tcp:127.0.0.1,1;Database=Unavailable;User Id=test;Password=test;Connect Timeout=1;ConnectRetryCount=0");
            builder.UseSetting("Database:ApplyMigrations", "false");
            builder.UseSetting("Deployment:UseHttpsRedirection", "false");
            if (keysPath is not null)
            {
                builder.UseSetting("DataProtection:KeysPath", keysPath);
            }
        }
    }
}
