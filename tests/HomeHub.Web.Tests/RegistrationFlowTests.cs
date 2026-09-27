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

public sealed partial class RegistrationFlowTests
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

    [Fact]
    public async Task AccountSettingsRenderAndProfileFormStillSaves()
    {
        using var factory = new RegistrationFactory();
        using var client = factory.Client();
        await Register(client, "settings@example.test", "Settings home", "");
        await factory.Confirm("settings@example.test");
        await Post(client, "/Account/Login", "login", new()
        {
            ["Input.Email"] = "settings@example.test", ["Input.Password"] = "StrongPassword123!"
        });

        string[] pages = ["", "/Email", "/ChangePassword", "/ExternalLogins", "/TwoFactorAuthentication",
            "/PersonalData", "/DeletePersonalData", "/EnableAuthenticator", "/ResetAuthenticator"];
        foreach (var page in pages)
        {
            var html = await client.GetStringAsync("/Account/Manage" + page);
            Assert.Contains("account-card-wide", html);
            Assert.Contains("aria-label=\"Account settings\"", html);
            Assert.DoesNotContain("form-floating", html);
            Assert.Contains("<h2", html);
        }

        var response = await Post(client, "/Account/Manage", "profile", new()
        {
            ["Input.PhoneNumber"] = "01234567890"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var updated = await client.GetStringAsync("/Account/Manage");
        Assert.Contains("01234567890", updated);
    }

    [Fact]
    public async Task SidebarOnlyAppearsOnProtectedPagesAndLogoutSignsOut()
    {
        using var factory = new RegistrationFactory();
        using var client = factory.Client();
        await Register(client, "sidebar@example.test", "Sidebar home", "");
        await factory.Confirm("sidebar@example.test");
        await Post(client, "/Account/Login", "login", new()
        {
            ["Input.Email"] = "sidebar@example.test", ["Input.Password"] = "StrongPassword123!"
        });

        foreach (var path in new[] { "/Home", "/Account/Manage", "/Account/Manage/Email" })
        {
            var html = await client.GetStringAsync(path);
            Assert.Contains("id=\"app-sidebar\"", html);
            Assert.Contains("aria-label=\"Toggle sidebar\"", html);
            Assert.Contains("sidebar@example.test", html);
            Assert.Contains("action=\"Account/Logout\"", html);
            Assert.Contains("aria-label=\"Account settings\"", html);
        }

        foreach (var path in new[] { "/", "/Account/Login", "/Account/Register", "/Account/ForgotPassword" })
        {
            Assert.DoesNotContain("id=\"app-sidebar\"", await client.GetStringAsync(path));
        }

        var home = await client.GetStringAsync("/Home");
        var token = Regex.Match(home, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        // Static SSR status-code re-execution surfaces the antiforgery rejection through TestServer.
        var missingToken = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["returnUrl"] = ""
            })));
        Assert.Contains("invalid anti-forgery token", missingToken.Message);
        Assert.Contains("id=\"app-sidebar\"", await client.GetStringAsync("/Home"));
        var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["returnUrl"] = "", ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        var protectedPage = await client.GetAsync("/Home");
        Assert.Contains("/Account/Login", protectedPage.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ConfigurationFormsPersistAndRejectMemberChanges()
    {
        using var factory = new RegistrationFactory();
        using var owner = factory.Client();
        await Register(owner, "owner@example.test", "Original", "");
        await factory.Confirm("owner@example.test");
        await Post(owner, "/Account/Login", "login", new()
        {
            ["Input.Email"] = "owner@example.test", ["Input.Password"] = "StrongPassword123!"
        });
        var response = await Post(owner, "/Home/Configure", "rename-home", new() { ["Rename.Name"] = "Updated" });
        Assert.Contains("Home name saved", await response.Content.ReadAsStringAsync());
        response = await Post(owner, "/Home/Configure", "add-floor", new() { ["Floor.Name"] = "Ground floor" });
        Assert.Contains("Floor added", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var floor = await database.HomeFloors.SingleAsync();
        response = await Post(owner, "/Home/Configure", "add-room", new()
        {
            ["Room.Name"] = "Kitchen", ["Room.FloorId"] = floor.Id.ToString()
        });
        Assert.Contains("Room added", await response.Content.ReadAsStringAsync());
        Assert.Contains("Kitchen", await owner.GetStringAsync("/Home/Configure"));
        Assert.Contains("Updated", await owner.GetStringAsync("/Home"));
        response = await Post(owner, "/Home/Configure", "add-room", new()
        {
            ["Room.Name"] = "Invalid", ["Room.FloorId"] = Guid.NewGuid().ToString()
        });
        Assert.Contains("Choose a floor in a home you own", await response.Content.ReadAsStringAsync());
        var invite = await Post(owner, "/Home", "invite-home", new());
        var code = Regex.Match(await invite.Content.ReadAsStringAsync(), "<code>([A-F0-9]{32})</code>").Groups[1].Value;
        using var member = factory.Client();
        await Register(member, "member@example.test", "", code);
        await factory.Confirm("member@example.test");
        await Post(member, "/Account/Login", "login", new()
        {
            ["Input.Email"] = "member@example.test", ["Input.Password"] = "StrongPassword123!"
        });
        var html = await member.GetStringAsync("/Home/Configure");
        Assert.Contains("Kitchen", html);
        Assert.DoesNotContain("Save home name", html);
        response = await Post(member, "/Home/Configure", "rename-home", new() { ["Rename.Name"] = "Hacked" });
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal("Updated", (await database.Homes.AsNoTracking().SingleAsync()).Name);
        Assert.Single(await database.HomeRooms.ToListAsync());
    }
    [Fact]
    public async Task MemberFormsPersistAndRemovalRevokesAnExistingLoginSession()
    {
        using var factory = new RegistrationFactory();
        using var owner = factory.Client();
        await Register(owner, "owner@example.test", "Family", "");
        await SignIn(factory, owner, "owner@example.test");
        var created = await Post(owner, "/Home/Members/New", "member-profile", new()
        {
            ["Input.Name"] = "Sam", ["Input.Colour"] = "#2563EB"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Contains("Sam", await owner.GetStringAsync("/Home/Members"));
        Guid profileId;
        using (var scope = factory.Services.CreateScope())
            profileId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().HomeMembers
                .Where(item => !item.IsAccountLinked).Select(item => item.Id).SingleAsync();
        var edited = await Post(owner, $"/Home/Members/Edit/{profileId}", "member-profile", new()
        {
            ["Input.Name"] = "Samantha", ["Input.Colour"] = ""
        });
        Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        Assert.Contains("Samantha", await owner.GetStringAsync("/Home/Members"));
        var zone = await Post(owner, "/Home/Configure", "home-timezone", new() { ["Zone.Id"] = "Europe/London" });
        Assert.Contains("Timezone saved", await zone.Content.ReadAsStringAsync());

        var invitation = await Post(owner, "/Home", "invite-home", new());
        var code = Regex.Match(await invitation.Content.ReadAsStringAsync(), "<code>([A-F0-9]{32})</code>").Groups[1].Value;
        using var member = factory.Client();
        await Register(member, "member@example.test", "", code);
        await SignIn(factory, member, "member@example.test");
        Assert.Contains("Samantha", await member.GetStringAsync("/Home/Members"));
        var forbiddenEdit = await Post(member, $"/Home/Members/Edit/{profileId}", "member-profile", new() { ["Input.Name"] = "Forbidden" });
        Assert.DoesNotContain("Save profile", await forbiddenEdit.Content.ReadAsStringAsync());
        Assert.Contains("Only the owner", await member.GetStringAsync($"/Home/Members/Remove/{profileId}"));
        await Post(member, $"/Home/Members/Remove/{profileId}", "remove-member", new() { ["Input.Confirmed"] = "true" });
        Assert.Contains("Samantha", await owner.GetStringAsync("/Home/Members"));
        Guid memberId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            memberId = await (from link in db.HomeMemberships join user in db.Users on link.UserId equals user.Id
                              where user.Email == "member@example.test" select link.MemberId).SingleAsync();
        }
        var confirmation = await owner.GetStringAsync($"/Home/Members/Remove/{memberId}");
        Assert.Contains("member@example.test", confirmation);
        var unconfirmed = await Post(owner, $"/Home/Members/Remove/{memberId}", "remove-member", new());
        Assert.Contains("Confirm the named member", await unconfirmed.Content.ReadAsStringAsync());
        Assert.Contains("Family", await member.GetStringAsync("/Home"));
        var removed = await Post(owner, $"/Home/Members/Remove/{memberId}", "remove-member", new() { ["Input.Confirmed"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.DoesNotContain("Samantha", await member.GetStringAsync("/Home/Members"));
        Assert.DoesNotContain("Family", await member.GetStringAsync("/Home/Configure"));
        Assert.Contains("Create a home or join one", await member.GetStringAsync("/Home"));
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/Account/Manage")).StatusCode);
        var retire = await Post(owner, $"/Home/Members/Remove/{profileId}", "remove-member", new() { ["Input.Confirmed"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, retire.StatusCode);
        Assert.DoesNotContain("Samantha", await owner.GetStringAsync("/Home/Members"));
    }

    [Fact]
    public async Task OwnerCanRevokeCodeAndRegistrationPreservesEnteredValues()
    {
        using var factory = new RegistrationFactory();
        using var owner = factory.Client();
        await Register(owner, "owner@example.test", "Family", "");
        await SignIn(factory, owner, "owner@example.test");
        var generated = await Post(owner, "/Home", "invite-home", new());
        var code = Regex.Match(await generated.Content.ReadAsStringAsync(), "<code>([A-F0-9]{32})</code>").Groups[1].Value;
        Guid id;
        using (var scope = factory.Services.CreateScope())
            id = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().HomeInvitations.Select(item => item.Id).SingleAsync();
        var revoked = await Post(owner, "/Home", "revoke-invitation", new() { ["Revoke.Id"] = id.ToString() });
        Assert.Contains("Revoked", await revoked.Content.ReadAsStringAsync());
        using var guest = factory.Client();
        var invalid = await Register(guest, "guest@example.test", "", code);
        var html = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("has been revoked", html);
        Assert.Contains("guest@example.test", html);
        Assert.DoesNotContain("Family", html);
    }

    [Fact]
    public async Task JoinValidationIsRateLimitedAcrossEntryPoints()
    {
        using var factory = new RegistrationFactory();
        using var client = factory.Client();
        await Register(client, "owner@example.test", "Home", "");
        await SignIn(factory, client, "owner@example.test");
        for (var attempt = 0; attempt < 9; attempt++)
        {
            var result = await Register(client, "guest@example.test", "", new string('A', 32));
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }
        var rejected = await Register(client, "guest@example.test", "", new string('A', 32));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        // The authenticated-home and external-registration paths use the same client budget.
        var home = await client.PostAsync("/Home", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.TooManyRequests, home.StatusCode);
        var external = await client.PostAsync("/Account/ExternalLogin", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.TooManyRequests, external.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Register")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.ToListAsync());
    }

    private static async Task SignIn(RegistrationFactory factory, HttpClient client, string email)
    {
        await factory.Confirm(email);
        var login = await Post(client, "/Account/Login", "login", new()
        {
            ["Input.Email"] = email, ["Input.Password"] = "StrongPassword123!"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.EndsWith("/Home", login.Headers.Location!.OriginalString);
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
