using HomeHub.Web.Data;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class RegistrationTests
{
    public static RegisterAccount.Request Request(string email = "owner@example.test") => new()
    {
        Email = email, Password = "HomeHub!123", ConfirmPassword = "HomeHub!123", Name = "Owner",
        Choice = HouseholdChoice.Create, HouseholdName = "Family"
    };

    [Fact]
    public async Task CreateRegistersOwnerAtomicallyAndRejectsDuplicateEmail()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var result = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(Request());
        Assert.True(result.Succeeded);
        var duplicate = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(Request());
        Assert.False(duplicate.Succeeded);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.True((await db.HouseholdMemberships.SingleAsync()).IsOwner);
    }

    [Fact]
    public async Task FailedJoinLeavesNoAccount()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var input = Request(); input.Choice = HouseholdChoice.Join; input.InvitationCode = "invalid";
        Assert.False((await app.Services.GetRequiredService<RegisterAccount>().RunAsync(input)).Succeeded);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task InvitationCanBeRedeemedOnlyOnce()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        await app.Services.GetRequiredService<RegisterAccount>().RunAsync(Request());
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new HouseholdInvitation { HouseholdId = (await db.Households.SingleAsync()).Id,
                CodeHash = HouseholdEnrollment.HashCode("one-use"), ExpiresAtUtc = DateTime.UtcNow.AddDays(7) });
            await db.SaveChangesAsync();
        }
        var first = Request("first@example.test"); first.Choice = HouseholdChoice.Join; first.InvitationCode = "one-use";
        Assert.True((await app.Services.GetRequiredService<RegisterAccount>().RunAsync(first)).Succeeded);
        var second = Request("second@example.test"); second.Choice = HouseholdChoice.Join; second.InvitationCode = "one-use";
        Assert.False((await app.Services.GetRequiredService<RegisterAccount>().RunAsync(second)).Succeeded);
        await using var check = app.Services.CreateAsyncScope();
        Assert.Equal(2, await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync());
    }
}
