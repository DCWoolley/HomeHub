using HomeHub.Web.Data;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageAccountMembers;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.MealPlanner;
using HomeHub.Web.Modules.MealPlanner.CreateMealFromPlanner;
using HomeHub.Web.Modules.MealPlanner.ManageMeals;
using HomeHub.Web.Modules.MealPlanner.ManagePlannerSettings;
using HomeHub.Web.Modules.MealPlanner.PlanWeek;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class PlannerSettingsTests
{
    [Fact]
    public async Task HouseholdSettingsDefaultToDinnerAndHideWithoutDeletingDisabledMeals()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
        var stranger = MembershipTests.Actor(foreign.UserId!);
        var input = RegistrationTests.Request("member@example.test"); input.Choice = HouseholdChoice.Join;
        input.InvitationCode = await app.Services.GetRequiredService<ManageInvitations>().CreateAsync(actor);
        var member = await register.RunAsync(input);
        var memberActor = MembershipTests.Actor(member.UserId!);
        var settings = app.Services.GetRequiredService<ManagePlannerSettings>();
        var defaults = await settings.GetAsync(actor);
        Assert.False(defaults.BreakfastEnabled); Assert.False(defaults.LunchEnabled); Assert.True(defaults.DinnerEnabled);
        var meals = app.Services.GetRequiredService<ManageMeals>();
        var meal = await meals.SaveAsync(actor, new() { Name = "Toast" });
        var planner = app.Services.GetRequiredService<PlanWeek>();
        var create = app.Services.GetRequiredService<CreateMealFromPlanner>();
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(actor, MealDay.Monday, MealTime.Breakfast, meal));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => create.RunAsync(actor, new() { Name = "New breakfast", Day = MealDay.Monday, Time = MealTime.Breakfast }));
        Assert.Single(await meals.ListAsync(actor));
        await settings.SaveAsync(memberActor, new() { BreakfastEnabled = true, LunchEnabled = true });
        Assert.True((await settings.GetAsync(actor)).BreakfastEnabled);
        Assert.True((await settings.GetAsync(actor)).LunchEnabled);
        Assert.False((await settings.GetAsync(stranger)).BreakfastEnabled);
        foreach (var day in Enum.GetValues<MealDay>())
            await planner.SetAsync(actor, day, MealTime.Breakfast, meal);
        await planner.SetAsync(actor, MealDay.Monday, MealTime.Dinner, meal);
        var breakfast = (await planner.GetAsync(actor)).First(x => x.Time == MealTime.Breakfast);
        await settings.SaveAsync(actor, new());
        Assert.Single(await planner.GetAsync(memberActor));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.SetAsync(memberActor, MealDay.Tuesday, MealTime.Breakfast, meal));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => planner.RemoveAsync(memberActor, breakfast.Id));
        await using (var scope = app.Services.CreateAsyncScope())
            Assert.Equal(8, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PlannedMeals.CountAsync());
        await settings.SaveAsync(memberActor, new() { BreakfastEnabled = true });
        Assert.Equal(8, (await planner.GetAsync(actor)).Count);
        await settings.SaveAsync(actor, new() { DinnerEnabled = false });
        Assert.Empty(await planner.GetAsync(actor));
        await settings.SaveAsync(actor, new());
        Assert.Single(await planner.GetAsync(actor));
        await app.Services.GetRequiredService<ManageAccountMembers>().RemoveAsync(actor, member.UserId!);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => settings.GetAsync(memberActor));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => settings.SaveAsync(memberActor, new()));
    }
}
