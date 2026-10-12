using HomeHub.Web.Data;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.Households.ManageAccountMembers;
using HomeHub.Web.Modules.MealPlanner;
using HomeHub.Web.Modules.MealPlanner.ManageMeals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class MealTests
{
    [Fact]
    public async Task CrudRejectsDuplicatesAndRechecksMembership()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
        var actor = MembershipTests.Actor(owner.UserId!);
        var input = RegistrationTests.Request("member@example.test"); input.Choice = HouseholdChoice.Join;
        input.InvitationCode = await app.Services.GetRequiredService<ManageInvitations>().CreateAsync(actor);
        var member = await register.RunAsync(input);
        var memberActor = MembershipTests.Actor(member.UserId!);
        var meals = app.Services.GetRequiredService<ManageMeals>();
        var id = await meals.SaveAsync(memberActor, new() { Name = " Pasta " });
        Assert.Equal("Pasta", (await meals.ListAsync(actor)).Single().Name);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(actor, new() { Name = "pasta" }));
        var other = await meals.SaveAsync(actor, new() { Name = "Soup" });
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(actor, new() { Id = other, Name = " PASTA " }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(actor, new() { Name = "  " }));
        await meals.SaveAsync(memberActor, new() { Id = id, Name = "Rice" });
        var stranger = MembershipTests.Actor(foreign.UserId!);
        Assert.Empty(await meals.ListAsync(stranger));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(stranger, new() { Id = id, Name = "Stolen" }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.PreviewDeleteAsync(stranger, id));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.DeleteAsync(stranger, id, true));
        await meals.SaveAsync(stranger, new() { Name = "Rice" });
        await app.Services.GetRequiredService<ManageAccountMembers>().RemoveAsync(actor, member.UserId!);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.ListAsync(memberActor));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(memberActor, new() { Name = "Toast" }));
        await meals.DeleteAsync(actor, other, false);
        Assert.Single(await meals.ListAsync(actor));
    }

    [Fact]
    public async Task DeletingAnInUseMealRequiresWarningAndCascadesOnlyItsOccurrences()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var owner = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var meals = app.Services.GetRequiredService<ManageMeals>();
        var id = await meals.SaveAsync(actor, new() { Name = "Pasta" });
        var other = await meals.SaveAsync(actor, new() { Name = "Salad" });
        Assert.Equal(0, (await meals.PreviewDeleteAsync(actor, id)).PlannedOccurrences);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var household = (await db.HouseholdMemberships.SingleAsync()).HouseholdId;
        var day = MealDay.Monday;
        db.PlannedMeals.AddRange(new PlannedMeal { HouseholdId = household, MealId = id, Day = day, Time = MealTime.Dinner },
            new PlannedMeal { HouseholdId = household, MealId = id, Day = MealDay.Friday, Time = MealTime.Lunch },
            new PlannedMeal { HouseholdId = household, MealId = other, Day = day, Time = MealTime.Dinner });
        await db.SaveChangesAsync();
        Assert.Equal(2, (await meals.PreviewDeleteAsync(actor, id)).PlannedOccurrences);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.DeleteAsync(actor, id, false));
        Assert.Equal(3, await db.PlannedMeals.CountAsync());
        await meals.DeleteAsync(actor, id, true);
        Assert.Equal(other, (await db.PlannedMeals.AsNoTracking().SingleAsync()).MealId);
        Assert.DoesNotContain(await meals.ListAsync(actor), x => x.Id == id);
    }
}
