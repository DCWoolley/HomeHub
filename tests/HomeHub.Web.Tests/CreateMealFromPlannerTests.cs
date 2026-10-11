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
        var date = new DateOnly(2026, 10, 12);
        var existing = await meals.SaveAsync(actor, new() { Name = "Toast" });
        await planner.SetAsync(actor, date, MealTime.Breakfast, existing);
        var result = await create.RunAsync(actor, new() { Name = " Eggs ", Date = date, Time = MealTime.Breakfast });
        Assert.False(result.Reused); Assert.Equal("Eggs", result.Name);
        Assert.Equal(2, (await meals.ListAsync(actor)).Count);
        Assert.Equal(2, (await planner.GetAsync(actor, date)).Count);
        var reused = await create.RunAsync(actor, new() { Name = " eggs ", Date = date, Time = MealTime.Breakfast });
        Assert.True(reused.Reused); Assert.True(reused.AlreadyPlanned); Assert.Equal(result.MealId, reused.MealId);
        Assert.Equal(2, (await planner.GetAsync(actor, date)).Count);
        var otherSlot = await create.RunAsync(actor, new() { Name = "EGGS", Date = date.AddDays(7), Time = MealTime.Dinner });
        Assert.True(otherSlot.Reused); Assert.False(otherSlot.AlreadyPlanned);
        Assert.Single(await planner.GetAsync(actor, date.AddDays(7)));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => create.RunAsync(actor, new() { Name = "", Date = date, Time = MealTime.Breakfast }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => create.RunAsync(actor, new() { Name = "Invalid", Date = date, Time = (MealTime)99 }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => create.RunAsync(new(), new() { Name = "Unauthorized", Date = date, Time = MealTime.Dinner }));
        Assert.Equal(2, (await meals.ListAsync(actor)).Count);
        var foreignResult = await create.RunAsync(stranger, new() { Name = "Eggs", Date = date, Time = MealTime.Breakfast });
        Assert.NotEqual(result.MealId, foreignResult.MealId);
        Assert.Equal(2, (await planner.GetAsync(actor, date)).Count);
    }
}
