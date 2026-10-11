using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.Households.ManageProfiles;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class ProfileTests
{
    [Fact]
    public async Task OrdinaryMembersCanManageProfilesButCannotChangeAccountAccessOrAnotherHousehold()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
        var code = await app.Services.GetRequiredService<ManageInvitations>().CreateAsync(MembershipTests.Actor(owner.UserId!));
        var input = RegistrationTests.Request("member@example.test"); input.Choice = HouseholdChoice.Join; input.InvitationCode = code;
        var member = await register.RunAsync(input);
        var profiles = app.Services.GetRequiredService<ManageProfiles>();
        var actor = MembershipTests.Actor(member.UserId!);
        await profiles.SaveAsync(actor, new() { Name = "Child" });
        var child = (await profiles.GetActiveMembersAsync(actor)).Single(x => !x.HasAccount);
        await profiles.SaveAsync(actor, new() { Id = child.Id, Name = "Updated child" });
        Assert.Contains(await profiles.GetActiveMembersAsync(actor), x => x.Name == "Updated child");
        await Assert.ThrowsAsync<HouseholdRuleException>(() => profiles.SaveAsync(MembershipTests.Actor(foreign.UserId!), new() { Id = child.Id, Name = "Intruder" }));
        var linked = (await profiles.GetActiveMembersAsync(actor)).First(x => x.HasAccount);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => profiles.RetireAsync(actor, linked.Id));
        await profiles.RetireAsync(actor, child.Id);
        Assert.DoesNotContain(await profiles.GetActiveMembersAsync(actor), x => x.Id == child.Id);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => profiles.SaveAsync(actor, new() { Id = child.Id, Name = "Restore" }));
    }
}
