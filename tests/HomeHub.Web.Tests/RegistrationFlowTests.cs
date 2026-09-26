using System.Net;
using System.Text.RegularExpressions;
using HomeHub.Infrastructure.Identity;
using HomeHub.Infrastructure.Persistence;
using HomeHub.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class RegistrationFlowTests
{
    [Fact]
    public async Task RegistrationCreatesOwnerWhoCanInviteAMember()
    {
        using var factory = new RegistrationFactory();
        using var owner = factory.Client();
        var response = await Register(owner, "owner@example.test", "Family home", "");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Account/RegisterConfirmation", response.Headers.Location!.OriginalString);
        await factory.Confirm("owner@example.test");
        var login = await Post(owner, "/Account/Login", "login", new()
        {
            ["Input.Email"] = "owner@example.test", ["Input.Password"] = "StrongPassword123!"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var homePage = await owner.GetStringAsync("/Home");
        Assert.Contains("Family home", homePage);
        var invitationResponse = await Post(owner, "/Home", "invite-home", new());
        Assert.Equal(HttpStatusCode.OK, invitationResponse.StatusCode);
        var invitationHtml = await invitationResponse.Content.ReadAsStringAsync();
        var code = Regex.Match(invitationHtml, "<code>([A-F0-9]{32})</code>").Groups[1].Value;
        Assert.Equal(32, code.Length);

        using var member = factory.Client();
        response = await Register(member, "member@example.test", "", code);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await factory.Confirm("member@example.test");
        await Post(member, "/Account/Login", "login", new()
        {
            ["Input.Email"] = "member@example.test", ["Input.Password"] = "StrongPassword123!"
        });
        homePage = await member.GetStringAsync("/Home");
        Assert.Contains("You are a member", homePage);
        Assert.DoesNotContain("Generate invitation code", homePage);

        using var other = factory.Client();
        response = await Register(other, "other@example.test", "", code);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("invalid, expired or already used", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await database.Users.CountAsync());
        Assert.Equal(2, await database.HomeMemberships.CountAsync());
    }

    [Fact]
    public async Task MissingHomeAndMissingAntiforgeryCannotCreateAccount()
    {
        using var factory = new RegistrationFactory();
        using var client = factory.Client();
        var response = await Register(client, "nobody@example.test", "", "");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Enter either a home name or an invitation code", await response.Content.ReadAsStringAsync());
        response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "register", ["Input.Email"] = "nobody@example.test",
            ["Input.Password"] = "StrongPassword123!", ["Input.ConfirmPassword"] = "StrongPassword123!", ["Input.HomeName"] = "Home"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.ToListAsync());
    }

    private static Task<HttpResponseMessage> Register(HttpClient client, string email, string name, string code) =>
        Post(client, "/Account/Register", "register", new()
        {
            ["Input.Email"] = email, ["Input.Password"] = "StrongPassword123!",
            ["Input.ConfirmPassword"] = "StrongPassword123!", ["Input.HomeName"] = name, ["Input.InvitationCode"] = code
        });

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, string handler, Dictionary<string, string> fields)
    {
        var html = await client.GetStringAsync(path);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        fields["_handler"] = handler;
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private sealed class RegistrationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        public RegistrationFactory() => connection.Open();

        public HttpClient Client()
        {
            var client = CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("http://localhost") });
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
            return client;
        }

        public async Task Confirm(string email)
        {
            using var scope = Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByEmailAsync(email))!;
            Assert.True((await users.ConfirmEmailAsync(user, await users.GenerateEmailConfirmationTokenAsync(user))).Succeeded);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Database:ApplyMigrations", "false");
            builder.UseSetting("Deployment:UseHttpsRedirection", "false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) connection.Dispose();
        }
    }
}
