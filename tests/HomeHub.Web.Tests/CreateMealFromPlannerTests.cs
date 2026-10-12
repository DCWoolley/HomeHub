using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.MealPlanner;
using HomeHub.Web.Modules.MealPlanner.ManageMeals;
using HomeHub.Web.Modules.MealPlanner.PlanWeek;
using HomeHub.Web.Modules.MealPlanner.CreateMealFromPlanner;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class CreateMealFromPlannerTests
{
    [Fact]
    public async Task CreationAndReusePreserveOtherMealsAndValidationMakesNoChanges()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
        var actor = MembershipTests.Actor(owner.UserId!);
        var stranger = MembershipTests.Actor(foreign.UserId!);
        var meals = app.Services.GetRequiredService<ManageMeals>();
        var planner = app.Services.GetRequiredService<PlanWeek>();
        var create = app.Services.GetRequiredService<CreateMealFromPlanner>();
        await app.Services.GetRequiredService<HomeHub.Web.Modules.MealPlanner.ManagePlannerSettings.ManagePlannerSettings>()
            .SaveAsync(actor, new() { BreakfastEnabled = true, LunchEnabled = true, DinnerEnabled = true });
        var day = MealDay.Monday;
        var existing = await meals.SaveAsync(actor, new() { Name = "Toast" });
        await planner.SetAsync(actor, day, MealTime.Breakfast, existing);
        var result = await create.RunAsync(actor, new() { Name = " Eggs ", Day = day, Time = MealTime.Breakfast });
        Assert.False(result.Reused); Assert.Equal("Eggs", result.Name);
        Assert.Equal(2, (await meals.ListAsync(actor)).Count);
        Assert.Equal(2, (await planner.GetAsync(actor)).Count);
        var reused = await create.RunAsync(actor, new() { Name = " eggs ", Day = day, Time = MealTime.Breakfast });
        Assert.True(reused.Reused); Assert.True(reused.AlreadyPlanned); Assert.Equal(result.MealId, reused.MealId);
        Assert.Equal(2, (await planner.GetAsync(actor)).Count);
        var otherSlot = await create.RunAsync(actor, new() { Name = "EGGS", Day = MealDay.Tuesday, Time = MealTime.Dinner });
        Assert.True(otherSlot.Reused); Assert.False(otherSlot.AlreadyPlanned);
        Assert.Single(await planner.GetAsync(actor), x => x.Day == MealDay.Tuesday);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => create.RunAsync(actor, new() { Name = "", Day = day, Time = MealTime.Breakfast }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => create.RunAsync(actor, new() { Name = "Invalid", Day = day, Time = (MealTime)99 }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => create.RunAsync(actor, new() { Name = "Invalid", Day = (MealDay)99, Time = MealTime.Dinner }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => create.RunAsync(new(), new() { Name = "Unauthorized", Day = day, Time = MealTime.Dinner }));
        Assert.Equal(2, (await meals.ListAsync(actor)).Count);
        var foreignResult = await create.RunAsync(stranger, new() { Name = "Eggs", Day = day, Time = MealTime.Dinner });
        Assert.NotEqual(result.MealId, foreignResult.MealId);
        Assert.Equal(3, (await planner.GetAsync(actor)).Count);
    }
}
