using System.Globalization;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageAccountMembers;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.MealPlanner.ManageIngredients;
using HomeHub.Web.Modules.MealPlanner.ManageIngredientUnits;
using HomeHub.Web.Modules.MealPlanner.ManageMeals;
using HomeHub.Web.Modules.MealPlanner.ManageRecipe;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class IngredientUnitTests
{
    [Fact]
    public async Task CustomUnitsAmountsAndRecipeQuantitiesKeepTheirMeaning()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var owner = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var units = app.Services.GetRequiredService<ManageIngredientUnits>();
        var ingredients = app.Services.GetRequiredService<ManageIngredients>();
        var recipes = app.Services.GetRequiredService<ManageRecipe>();
        var grams = await units.CreateAsync(actor, new() { Name = " grams " });
        Assert.Equal(grams, await units.CreateAsync(actor, new() { Name = "GRAMS" }));
        Assert.Equal("grams", (await units.ListAsync(actor)).Single().Name);
        var packets = await units.CreateAsync(actor, new() { Name = "packets" });
        var pasta = await ingredients.SaveAsync(actor, new() { Name = "Pasta", UnitId = grams, Amount = "500" });
        var stored = (await ingredients.ListAsync(actor)).Single();
        Assert.Equal(500m, stored.Amount); Assert.Equal(grams, stored.UnitId); Assert.Equal("grams", stored.Unit);
        var meal = await app.Services.GetRequiredService<ManageMeals>().SaveAsync(actor, new() { Name = "Bake" });
        await recipes.SaveAsync(actor, meal, pasta, 250m, expectedUnitId: grams);
        Assert.Equal(250m, (await recipes.GetAsync(actor, meal)).Items.Single().Quantity);
        Assert.Equal("grams", (await recipes.GetAsync(actor, meal)).Items.Single().Unit);
        await ingredients.SaveAsync(actor, new() { Id = pasta, Name = "Pasta", UnitId = grams, Amount = "750" });
        Assert.Equal(250m, (await recipes.GetAsync(actor, meal)).Items.Single().Quantity);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(actor, new() { Id = pasta, Name = "Pasta", UnitId = packets }));
        Assert.Equal(grams, (await ingredients.ListAsync(actor)).Single().UnitId);
        await recipes.RemoveAsync(actor, meal, pasta);
        await ingredients.SaveAsync(actor, new() { Id = pasta, Name = "Pasta", UnitId = packets, Amount = "1" });
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(actor, meal, pasta, 250m, expectedUnitId: grams));
        Assert.Empty((await recipes.GetAsync(actor, meal)).Items);
        await recipes.SaveAsync(actor, meal, pasta, 0.5m, expectedUnitId: packets);
        Assert.Equal("packets", (await recipes.GetAsync(actor, meal)).Items.Single().Unit);
        foreach (var invalid in new[] { "", "abc", "0", "-1", "100000000", "0.00001", "0.500000000000000000000000000001" })
            await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(actor, new() { Name = "Invalid", UnitId = grams, Amount = invalid }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(actor, new() { Name = "Missing unit" }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(actor, new() { Name = "Unknown unit", UnitId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => units.CreateAsync(actor, new() { Name = " " }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => units.CreateAsync(actor, new() { Name = new string('x', 51) }));
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var id = await ingredients.SaveAsync(actor, new() { Name = "Salt", UnitId = grams, Amount = "0,1234" });
            Assert.Equal(0.1234m, (await ingredients.ListAsync(actor)).Single(x => x.Id == id).Amount);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task UnitCreationAndSelectionRecheckMembershipAndHousehold()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
        var actor = MembershipTests.Actor(owner.UserId!);
        var stranger = MembershipTests.Actor(foreign.UserId!);
        var units = app.Services.GetRequiredService<ManageIngredientUnits>();
        var ingredients = app.Services.GetRequiredService<ManageIngredients>();
        var input = RegistrationTests.Request("member@example.test"); input.Choice = HouseholdChoice.Join;
        input.InvitationCode = await app.Services.GetRequiredService<ManageInvitations>().CreateAsync(actor);
        var member = await register.RunAsync(input);
        var memberActor = MembershipTests.Actor(member.UserId!);
        var grams = await units.CreateAsync(memberActor, new() { Name = "grams" });
        var foreignGrams = await units.CreateAsync(stranger, new() { Name = "grams" });
        Assert.NotEqual(grams, foreignGrams);
        Assert.Equal(grams, (await units.ListAsync(actor)).Single().Id);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(actor, new() { Name = "Pasta", UnitId = foreignGrams, Amount = "500" }));
        await app.Services.GetRequiredService<ManageAccountMembers>().RemoveAsync(actor, member.UserId!);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => units.ListAsync(memberActor));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => units.CreateAsync(memberActor, new() { Name = "cups" }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => units.ListAsync(new()));
    }
}
