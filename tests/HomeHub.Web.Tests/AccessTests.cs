using System.Net;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageAccountMembers;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.Households.Onboarding;
using HomeHub.Web.Modules.Households.GetHousehold;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class AccessTests
{
    [Fact]
    public async Task OldCookieLosesAccessAfterRemovalAndExistingAccountCanJoinAgain()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var ownerActor = MembershipTests.Actor(owner.UserId!);
        var invites = app.Services.GetRequiredService<ManageInvitations>();
        var code = await invites.CreateAsync(ownerActor);
        using var browser = app.Browser();
        var response = await BrowserForms.PostAsync(browser, "/Account/Register", "register", new()
        {
            ["Input.Email"] = "browser@example.test", ["Input.Name"] = "Browser member",
            ["Input.Password"] = "HomeHub!123", ["Input.ConfirmPassword"] = "HomeHub!123",
            ["Input.Choice"] = "Join", ["Input.InvitationCode"] = code
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Dashboard")).StatusCode);
        var accounts = app.Services.GetRequiredService<ManageAccountMembers>();
        var member = (await accounts.ListAsync(ownerActor)).Single(x => !x.IsOwner);
        await accounts.RemoveAsync(ownerActor, member.UserId);
        var protectedResponse = await browser.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, protectedResponse.StatusCode);
        Assert.Equal("/Household/Onboarding", protectedResponse.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Household/Onboarding")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Account/Manage")).StatusCode);
        var actor = MembershipTests.Actor(member.UserId);
        Assert.Null(await app.Services.GetRequiredService<GetHousehold>().RunAsync(actor));
        await app.Services.GetRequiredService<Onboarding>().RunAsync(actor, new() { Name = "Returning", Choice = HouseholdChoice.Join, InvitationCode = await invites.CreateAsync(ownerActor) });
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Dashboard")).StatusCode);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => app.Services.GetRequiredService<Onboarding>()
            .RunAsync(actor, new() { Name = "Two homes", HouseholdName = "Other" }));
    }

    [Theory]
    [InlineData("/Dashboard")]
    [InlineData("/Meals")]
    [InlineData("/MealPlanner")]
    [InlineData("/Household")]
    [InlineData("/Household/Members")]
    [InlineData("/Household/Invitations")]
    [InlineData("/Household/Accounts")]
    [InlineData("/Household/Ownership")]
    [InlineData("/Household/Leave")]
    [InlineData("/Household/Onboarding")]
    public async Task HouseholdRoutesRequireAuthentication(string path)
    {
        using var app = new RestartFactory(); using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync(path)).StatusCode);
    }
}
