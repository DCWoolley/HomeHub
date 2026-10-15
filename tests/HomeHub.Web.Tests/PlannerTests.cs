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
    public async Task SingleSchedulePreservesMultipleMealsAndOnlySelectedOccurrencesChange()
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
        await app.Services.GetRequiredService<HomeHub.Web.Modules.MealPlanner.ManagePlannerSettings.ManagePlannerSettings>()
            .SaveAsync(actor, new() { BreakfastEnabled = true, LunchEnabled = true, DinnerEnabled = true });
        var first = await meals.SaveAsync(actor, new() { Name = "Eggs" });
        var second = await meals.SaveAsync(actor, new() { Name = "Toast" });
        var third = await meals.SaveAsync(actor, new() { Name = "Fruit" });
        var day = MealDay.Monday;
        await planner.SetAsync(memberActor, day, MealTime.Breakfast, first);
        await planner.SetAsync(memberActor, day, MealTime.Breakfast, second);
        await planner.SetAsync(actor, MealDay.Sunday, MealTime.Dinner, first);
        await planner.SetAsync(actor, MealDay.Tuesday, MealTime.Lunch, first);
        var entries = await planner.GetAsync(actor);
        Assert.Equal(4, entries.Count);
        Assert.Contains(entries, x => x.Day == MealDay.Sunday);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(actor, day, MealTime.Breakfast, first));
        var entry = entries.Single(x => x.Day == day && x.MealId == first);
        await planner.SetAsync(actor, day, MealTime.Breakfast, third, entry.Id);
        Assert.Equal(4, (await planner.GetAsync(memberActor)).Count);
        Assert.Empty(await planner.GetAsync(stranger));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(stranger, day, MealTime.Breakfast, first));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.RemoveAsync(stranger, entry.Id));
        var foreignMeal = await meals.SaveAsync(stranger, new() { Name = "Other" });
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(actor, day, MealTime.Breakfast, foreignMeal, entry.Id));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(actor, day, (MealTime)99, first));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(actor, (MealDay)99, MealTime.Dinner, first));
        await planner.RemoveAsync(memberActor, entry.Id);
        Assert.Equal(3, (await planner.GetAsync(actor)).Count);
        await meals.DeleteAsync(actor, first, true);
        Assert.Single(await planner.GetAsync(actor));
        Assert.DoesNotContain(await planner.GetAsync(actor), x => x.MealId == first);
        await app.Services.GetRequiredService<ManageAccountMembers>().RemoveAsync(actor, member.UserId!);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.GetAsync(memberActor));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(memberActor, day, MealTime.Dinner, second));
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
        var day = MealDay.Monday;
        await app.Services.GetRequiredService<PlanWeek>().SetAsync(actor, day, MealTime.Dinner, meal);
        var html = await browser.GetStringAsync("/MealPlanner");
        Assert.Contains("<!--Blazor:{\"type\":\"server\"", html);
        Assert.Contains("<fieldset class=\"meal-controls\" disabled", html);
        foreach (var label in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" }) Assert.Contains(label, html);
        Assert.Contains("Pasta &amp; salad", html);
        Assert.DoesNotContain("Breakfast", html); Assert.DoesNotContain("Lunch", html); Assert.Contains("Dinner", html);
        Assert.Contains("Planner settings", html);
        var settingsHtml = await browser.GetStringAsync("/MealPlanner/Settings");
        Assert.Contains("Breakfast", settingsHtml); Assert.Contains("Lunch", settingsHtml); Assert.Contains("Dinner", settingsHtml);
        Assert.DoesNotContain("Previous week", html);
        Assert.DoesNotContain("Next week", html);
        Assert.DoesNotContain("type=\"date\"", html);
        var mealsHtml = await browser.GetStringAsync("/Meals");
        Assert.Contains("Pasta &amp; salad", mealsHtml);
        Assert.Contains("<!--Blazor:{\"type\":\"server\"", mealsHtml);
        Assert.Matches("<fieldset[^>]*disabled[^>]*aria-label=\"Meal management\"", mealsHtml);
        var member = (await app.Services.GetRequiredService<ManageAccountMembers>().ListAsync(actor)).Single(x => !x.IsOwner);
        await app.Services.GetRequiredService<ManageAccountMembers>().RemoveAsync(actor, member.UserId);
        foreach (var route in new[] { "/Meals", "/MealPlanner", "/MealPlanner/Settings" })
        {
            var denied = await browser.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Equal("/Household/Onboarding", denied.Headers.Location!.OriginalString);
        }
    }
}
