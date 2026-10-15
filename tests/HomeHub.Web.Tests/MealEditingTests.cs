using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.MealPlanner.ManageIngredients;
using HomeHub.Web.Modules.MealPlanner.ManageIngredientUnits;
using HomeHub.Web.Modules.MealPlanner.ManageMeals;
using HomeHub.Web.Modules.MealPlanner.ManageRecipe;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class MealEditingTests
{
    [Fact]
    public async Task CompleteRecipeSavesRejectForeignAndRevokedHouseholdAccess()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
        var actor = MembershipTests.Actor(owner.UserId!);
        var stranger = MembershipTests.Actor(foreign.UserId!);
        var join = RegistrationTests.Request("member@example.test");
        join.Choice = HouseholdChoice.Join;
        join.InvitationCode = await app.Services.GetRequiredService<HomeHub.Web.Modules.Households.ManageInvitations.ManageInvitations>().CreateAsync(actor);
        var member = await register.RunAsync(join);
        var memberActor = MembershipTests.Actor(member.UserId!);
        var unit = await app.Services.GetRequiredService<ManageIngredientUnits>().CreateAsync(stranger, new() { Name = "grams" });
        var ingredient = await app.Services.GetRequiredService<ManageIngredients>().SaveAsync(stranger, new() { Name = "Foreign rice", UnitId = unit });
        var meals = app.Services.GetRequiredService<ManageMeals>();
        var id = await meals.SaveAsync(actor, new() { Name = "Takeaway", Recipe = [] });
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(actor, new() { Id = id, Name = "Stolen", Recipe = [new(ingredient, unit, "1")] }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(stranger, new() { Id = id, Name = "Stolen", Recipe = [] }));
        await app.Services.GetRequiredService<HomeHub.Web.Modules.Households.ManageAccountMembers.ManageAccountMembers>().RemoveAsync(actor, member.UserId!);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(memberActor, new() { Id = id, Name = "Stolen", Recipe = [] }));
        Assert.Equal("Takeaway", (await meals.ListAsync(actor)).Single().Name);
        Assert.Empty((await app.Services.GetRequiredService<ManageRecipe>().GetAsync(actor, id)).Items);
    }

    [Fact]
    public async Task MealNameAndRecipeSaveTogetherAndFailedDraftsChangeNothing()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var owner = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var units = app.Services.GetRequiredService<ManageIngredientUnits>();
        var grams = await units.CreateAsync(actor, new() { Name = "grams" });
        var packets = await units.CreateAsync(actor, new() { Name = "packets" });
        var ingredients = app.Services.GetRequiredService<ManageIngredients>();
        var rice = await ingredients.SaveAsync(actor, new() { Name = "Rice", UnitId = grams, Amount = "1000" });
        var meals = app.Services.GetRequiredService<ManageMeals>();
        var recipes = app.Services.GetRequiredService<ManageRecipe>();
        var input = new ManageMeals.Request { Name = "Rice bowl", Recipe = [new(rice, grams, "250")] };
        var id = await meals.SaveAsync(actor, input);
        Assert.Equal(250m, (await recipes.GetAsync(actor, id)).Items.Single().Quantity);
        input.Id = id; input.Name = "Changed"; input.Recipe = [new(rice, grams, "0.00001")];
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(actor, input));
        Assert.Equal("Rice bowl", (await recipes.GetAsync(actor, id)).Name);
        Assert.Equal(250m, (await recipes.GetAsync(actor, id)).Items.Single().Quantity);
        input.Recipe = [new(rice, grams, "125"), new(rice, grams, "250")];
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(actor, input));
        input.Id = null; input.Name = "Orphan"; input.Recipe = [new(Guid.NewGuid(), grams, "1")];
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(actor, input));
        Assert.Single(await meals.ListAsync(actor));
        await ingredients.SaveAsync(actor, new() { Id = rice, Name = "Rice", UnitId = grams, Amount = "500" });
        input.Id = id; input.Name = "Rice bowl"; input.Recipe = [];
        await meals.SaveAsync(actor, input);
        Assert.Empty((await recipes.GetAsync(actor, id)).Items);
        await ingredients.SaveAsync(actor, new() { Id = rice, Name = "Rice", UnitId = packets, Amount = "1" });
        input.Name = "Stale"; input.Recipe = [new(rice, grams, "125")];
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.SaveAsync(actor, input));
        Assert.Equal("Rice bowl", (await recipes.GetAsync(actor, id)).Name);
        Assert.Empty((await recipes.GetAsync(actor, id)).Items);
    }

    [Fact]
    public async Task PackagePriceCanBeUnknownFreeOrKnownAndInvalidEditsPreserveIt()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var owner = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var grams = await app.Services.GetRequiredService<ManageIngredientUnits>().CreateAsync(actor, new() { Name = "grams" });
        var ingredients = app.Services.GetRequiredService<ManageIngredients>();
        var input = new ManageIngredients.Request { Name = "Rice", UnitId = grams, Amount = "1000" };
        input.Id = await ingredients.SaveAsync(actor, input);
        Assert.Null((await ingredients.ListAsync(actor)).Single().PackagePrice);
        input.PackagePrice = "0.00";
        await ingredients.SaveAsync(actor, input);
        Assert.Equal(0m, (await ingredients.ListAsync(actor)).Single().PackagePrice);
        input.PackagePrice = "1.50";
        await ingredients.SaveAsync(actor, input);
        Assert.Equal(1.50m, (await ingredients.ListAsync(actor)).Single().PackagePrice);
        foreach (var invalid in new[] { "-1", "abc", "1.001", "1.500", "100000000", "1e2" })
        {
            input.PackagePrice = invalid;
            await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(actor, input));
            Assert.Equal(1.50m, (await ingredients.ListAsync(actor)).Single().PackagePrice);
        }
        input.PackagePrice = "";
        await ingredients.SaveAsync(actor, input);
        Assert.Null((await ingredients.ListAsync(actor)).Single().PackagePrice);
    }
}
