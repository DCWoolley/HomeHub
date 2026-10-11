using HomeHub.Web.Data;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Account.Manage.DeletePersonalData;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.Households.TransferOwnership;
using HomeHub.Web.Modules.Households.LeaveHousehold;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class OwnershipTests
{
    [Fact]
    public async Task OwnerMustTransferBeforeLeavingAndCannotDeleteAccountToBypassRule()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var leave = app.Services.GetRequiredService<LeaveHousehold>();
        await Assert.ThrowsAsync<HouseholdRuleException>(() => leave.RunAsync(actor));
        Assert.NotNull(await app.Services.GetRequiredService<DeleteAccount>().RunAsync(actor, "HomeHub!123"));
        var invite = await app.Services.GetRequiredService<ManageInvitations>().CreateAsync(actor);
        var join = RegistrationTests.Request("successor@example.test"); join.Choice = HouseholdChoice.Join; join.InvitationCode = invite;
        var successor = await register.RunAsync(join);
        var transfer = app.Services.GetRequiredService<TransferOwnership>();
        await Assert.ThrowsAsync<HouseholdRuleException>(() => transfer.RunAsync(MembershipTests.Actor(successor.UserId!), owner.UserId!));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => transfer.RunAsync(actor, "nonexistent"));
        await transfer.RunAsync(actor, successor.UserId!);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => transfer.RunAsync(actor, owner.UserId!));
        await leave.RunAsync(actor);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = await db.HouseholdMemberships.SingleAsync();
        Assert.True(membership.IsOwner); Assert.Equal(successor.UserId, membership.UserId);
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Null(await app.Services.GetRequiredService<DeleteAccount>().RunAsync(actor, "HomeHub!123"));
    }
}
