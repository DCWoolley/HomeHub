using System.Net;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task SchemaEnforcesSingleOwnerAndOneHouseholdPerAccount()
    {
        using var app = new RestartFactory();
        await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var home = new Household { Name = "Family" };
        var first = new HouseholdMember { HouseholdId = home.Id, Name = "First" };
        var second = new HouseholdMember { HouseholdId = home.Id, Name = "Second" };
        db.AddRange(home, first, second, new ApplicationUser { Id = "one" }, new ApplicationUser { Id = "two" });
        await db.SaveChangesAsync();
        db.Add(new HouseholdMembership { UserId = "one", HouseholdId = home.Id, MemberId = first.Id, IsOwner = true });
        await db.SaveChangesAsync();
        db.Add(new HouseholdMembership { UserId = "two", HouseholdId = home.Id, MemberId = second.Id, IsOwner = true });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Add(new HouseholdMembership { UserId = "one", HouseholdId = home.Id, MemberId = second.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    public async Task PublicRoutesRenderOnLanHttp(string path)
    {
        using var app = new RestartFactory();
        await app.InitializeAsync();
        using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task AccountManagementRequiresLogin()
    {
        using var app = new RestartFactory();
        using var browser = app.Browser();
        var response = await browser.GetAsync("/Account/Manage");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }
}
