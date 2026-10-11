using HomeHub.Web.Data;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.Households.ManageProfiles;
using HomeHub.Web.Modules.Households.TransferOwnership;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class EdgeCaseTests
{
    [Fact]
    public async Task ExpiryIsExclusiveAndIdentityFailureDoesNotConsumeInvitation()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var invites = app.Services.GetRequiredService<ManageInvitations>();
        var code = await invites.CreateAsync(MembershipTests.Actor(owner.UserId!));
        var input = RegistrationTests.Request("join@example.test");
        input.Choice = HouseholdChoice.Join; input.InvitationCode = code;
        input.Password = input.ConfirmPassword = "weakpassword";
        Assert.False((await register.RunAsync(input)).Succeeded);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null((await db.HouseholdInvitations.SingleAsync()).UsedAtUtc);
            Assert.Equal(1, await db.Users.CountAsync());
            var cutoff = DateTime.UtcNow;
            await db.HouseholdInvitations.ExecuteUpdateAsync(set => set.SetProperty(x => x.ExpiresAtUtc, cutoff));
            await using var transaction = await db.Database.BeginTransactionAsync();
            await Assert.ThrowsAsync<HouseholdRuleException>(() => HouseholdEnrollment.ApplyAsync(db, owner.UserId! + "-new",
                "New", HouseholdChoice.Join, null, code, cutoff, default));
        }
        input.Password = input.ConfirmPassword = "HomeHub!123";
        Assert.False((await register.RunAsync(input)).Succeeded);
    }

    [Fact]
    public async Task TransferRejectsForeignAccountsAndNonLoginProfilesAndMutationsRejectAnonymousActors()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
        var actor = MembershipTests.Actor(owner.UserId!);
        var profiles = app.Services.GetRequiredService<ManageProfiles>();
        await profiles.SaveAsync(actor, new() { Name = "Child" });
        var child = (await profiles.GetActiveMembersAsync(actor)).Single(x => !x.HasAccount);
        var transfer = app.Services.GetRequiredService<TransferOwnership>();
        await Assert.ThrowsAsync<HouseholdRuleException>(() => transfer.RunAsync(actor, foreign.UserId!));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => transfer.RunAsync(actor, child.Id.ToString()));
        Assert.Empty(await transfer.CandidatesAsync(actor));
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.UserId!)]));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => profiles.SaveAsync(anonymous, new() { Name = "Forged" }));
    }
}
