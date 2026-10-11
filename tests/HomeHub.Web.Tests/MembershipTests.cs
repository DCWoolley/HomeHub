using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.Households.ManageAccountMembers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class MembershipTests
{
    public static ClaimsPrincipal Actor(string userId) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"));

    [Fact]
    public async Task OwnerInvitesOnceAndCanRemoveAccessWithoutDeletingAccount()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var registration = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await registration.RunAsync(RegistrationTests.Request());
        var actor = Actor(owner.UserId!);
        var invites = app.Services.GetRequiredService<ManageInvitations>();
        var code = await invites.CreateAsync(actor);
        var join = RegistrationTests.Request("joined@example.test"); join.Choice = HouseholdChoice.Join; join.InvitationCode = code;
        var joined = await registration.RunAsync(join); Assert.True(joined.Succeeded);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => invites.CreateAsync(Actor(joined.UserId!)));
        var accounts = app.Services.GetRequiredService<ManageAccountMembers>();
        await Assert.ThrowsAsync<HouseholdRuleException>(() => accounts.RemoveAsync(Actor(joined.UserId!), owner.UserId!));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => accounts.RemoveAsync(actor, owner.UserId!));
        await accounts.RemoveAsync(actor, joined.UserId!);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => accounts.ListAsync(Actor(joined.UserId!)));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal(1, await db.HouseholdMemberships.CountAsync());
        Assert.Equal(1, await db.HouseholdMembers.CountAsync(x => x.IsRetired));
    }

    [Fact]
    public async Task RevokedExpiredAndForeignInvitationsAreRejected()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var registration = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await registration.RunAsync(RegistrationTests.Request());
        var other = await registration.RunAsync(RegistrationTests.Request("other@example.test"));
        var invitations = app.Services.GetRequiredService<ManageInvitations>();
        var code = await invitations.CreateAsync(Actor(owner.UserId!));
        var invitation = (await invitations.ListAsync(Actor(owner.UserId!))).Single();
        Assert.InRange(invitation.ExpiresAtUtc, DateTime.UtcNow.AddDays(7).AddSeconds(-10), DateTime.UtcNow.AddDays(7));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => invitations.RevokeAsync(Actor(other.UserId!), invitation.Id));
        await invitations.RevokeAsync(Actor(owner.UserId!), invitation.Id);
        var join = RegistrationTests.Request("join@example.test"); join.Choice = HouseholdChoice.Join; join.InvitationCode = code;
        Assert.False((await registration.RunAsync(join)).Succeeded);
        var expired = await invitations.CreateAsync(Actor(owner.UserId!));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.HouseholdInvitations.Where(x => x.CodeHash == HouseholdEnrollment.HashCode(expired))
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ExpiresAtUtc, DateTime.UtcNow.AddSeconds(-1)));
        }
        join.InvitationCode = expired;
        Assert.False((await registration.RunAsync(join)).Succeeded);
    }
}
