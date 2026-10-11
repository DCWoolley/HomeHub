using System.Net;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageAccountMembers;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.MealPlanner;
using HomeHub.Web.Modules.MealPlanner.ManageMeals;
using HomeHub.Web.Modules.MealPlanner.PlanWeek;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class PlannerTests
{
    [Fact]
    public async Task MultipleMealsPersistAcrossWeeksAndOnlySelectedOccurrencesChange()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var input = RegistrationTests.Request("member@example.test"); input.Choice = HouseholdChoice.Join;
        input.InvitationCode = await app.Services.GetRequiredService<ManageInvitations>().CreateAsync(actor);
        var member = await register.RunAsync(input);
        var memberActor = MembershipTests.Actor(member.UserId!);
        var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
        var stranger = MembershipTests.Actor(foreign.UserId!);
        var meals = app.Services.GetRequiredService<ManageMeals>();
        var planner = app.Services.GetRequiredService<PlanWeek>();
        var first = await meals.SaveAsync(actor, new() { Name = "Eggs" });
        var second = await meals.SaveAsync(actor, new() { Name = "Toast" });
        var third = await meals.SaveAsync(actor, new() { Name = "Fruit" });
        var date = new DateOnly(2026, 10, 12);
        Assert.Equal(date, PlanWeek.Monday(date.AddDays(6)));
        await planner.SetAsync(memberActor, date, MealTime.Breakfast, first);
        await planner.SetAsync(memberActor, date, MealTime.Breakfast, second);
        await planner.SetAsync(actor, date.AddDays(6), MealTime.Dinner, first);
        await planner.SetAsync(actor, date.AddDays(7), MealTime.Lunch, first);
        var entries = await planner.GetAsync(actor, date.AddDays(6));
        Assert.Equal(3, entries.Count);
        Assert.Single(await planner.GetAsync(actor, date.AddDays(7)));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(actor, date, MealTime.Breakfast, first));
        var entry = entries.Single(x => x.Date == date && x.MealId == first);
        await planner.SetAsync(actor, date, MealTime.Breakfast, third, entry.Id);
        Assert.Equal(3, (await planner.GetAsync(memberActor, date)).Count);
        Assert.Empty(await planner.GetAsync(stranger, date));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(stranger, date, MealTime.Breakfast, first));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.RemoveAsync(stranger, entry.Id));
        var foreignMeal = await meals.SaveAsync(stranger, new() { Name = "Other" });
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(actor, date, MealTime.Breakfast, foreignMeal, entry.Id));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(actor, date, (MealTime)99, first));
        await planner.RemoveAsync(memberActor, entry.Id);
        Assert.Equal(2, (await planner.GetAsync(actor, date)).Count);
        await meals.DeleteAsync(actor, first, true);
        Assert.Single(await planner.GetAsync(actor, date));
        Assert.Empty(await planner.GetAsync(actor, date.AddDays(7)));
        await app.Services.GetRequiredService<ManageAccountMembers>().RemoveAsync(actor, member.UserId!);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.GetAsync(memberActor, date));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(memberActor, date, MealTime.Dinner, second));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.RemoveAsync(memberActor, entry.Id));
    }

    [Fact]
    public async Task AuthenticatedMealRoutesRenderAndRedirectAfterMembershipRemoval()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var owner = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var code = await app.Services.GetRequiredService<ManageInvitations>().CreateAsync(actor);
        using var browser = app.Browser();
        var response = await BrowserForms.PostAsync(browser, "/Account/Register", "register", new()
        {
            ["Input.Email"] = "browser@example.test", ["Input.Name"] = "Browser member",
            ["Input.Password"] = "HomeHub!123", ["Input.ConfirmPassword"] = "HomeHub!123",
            ["Input.Choice"] = "Join", ["Input.InvitationCode"] = code
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var meal = await app.Services.GetRequiredService<ManageMeals>().SaveAsync(actor, new() { Name = "Pasta & salad" });
        var week = PlanWeek.Monday(DateOnly.FromDateTime(DateTime.Today));
        await app.Services.GetRequiredService<PlanWeek>().SetAsync(actor, week, MealTime.Dinner, meal);
        var html = await browser.GetStringAsync("/MealPlanner");
        Assert.Contains("<!--Blazor:{\"type\":\"server\"", html);
        foreach (var day in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" }) Assert.Contains(day, html);
        Assert.Contains("Pasta &amp; salad", html);
        Assert.Contains("Breakfast", html); Assert.Contains("Lunch", html); Assert.Contains("Dinner", html);
        var mealsHtml = await browser.GetStringAsync("/Meals");
        Assert.Contains("Pasta &amp; salad", mealsHtml);
        Assert.Contains("<!--Blazor:{\"type\":\"server\"", mealsHtml);
        var member = (await app.Services.GetRequiredService<ManageAccountMembers>().ListAsync(actor)).Single(x => !x.IsOwner);
        await app.Services.GetRequiredService<ManageAccountMembers>().RemoveAsync(actor, member.UserId);
        foreach (var route in new[] { "/Meals", "/MealPlanner" })
        {
            var denied = await browser.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Equal("/Household/Onboarding", denied.Headers.Location!.OriginalString);
        }
    }
}
