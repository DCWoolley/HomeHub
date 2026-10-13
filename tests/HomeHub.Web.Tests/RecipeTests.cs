using System.Globalization;
using System.Net;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageAccountMembers;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.MealPlanner;
using HomeHub.Web.Modules.MealPlanner.ManageIngredients;
using HomeHub.Web.Modules.MealPlanner.ManageIngredientUnits;
using HomeHub.Web.Modules.MealPlanner.ManageMeals;
using HomeHub.Web.Modules.MealPlanner.ManageRecipe;
using HomeHub.Web.Modules.MealPlanner.PlanWeek;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class RecipeTests
{
    [Fact]
    public async Task RecipeCrudPreservesSharedIngredientsMealsAndSchedule()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var owner = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var unit = await app.Services.GetRequiredService<ManageIngredientUnits>().CreateAsync(actor, new() { Name = "packets" });
        var ingredients = app.Services.GetRequiredService<ManageIngredients>();
        var meals = app.Services.GetRequiredService<ManageMeals>();
        var recipes = app.Services.GetRequiredService<ManageRecipe>();
        var planner = app.Services.GetRequiredService<PlanWeek>();
        var pasta = await ingredients.SaveAsync(actor, new() { UnitId = unit, Name = " Pasta " });
        Assert.Equal("Pasta", (await ingredients.ListAsync(actor)).Single().Name);
        var sauce = await ingredients.SaveAsync(actor, new() { UnitId = unit, Name = "Sauce" });
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(actor, new() { UnitId = unit, Name = "pasta" }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(actor, new() { UnitId = unit, Id = sauce, Name = " PASTA " }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(actor, new() { UnitId = unit, Name = "  " }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(actor, new() { UnitId = unit, Name = new string('x', 101) }));
        var meal = await meals.SaveAsync(actor, new() { Name = "Pasta bake" });
        var other = await meals.SaveAsync(actor, new() { Name = "Pasta salad" });
        Assert.Empty((await recipes.GetAsync(actor, meal)).Items);
        await planner.SetAsync(actor, MealDay.Monday, MealTime.Dinner, meal);
        await planner.SetAsync(actor, MealDay.Monday, MealTime.Dinner, other);
        await recipes.SaveAsync(actor, meal, pasta, 0.5m);
        await recipes.SaveAsync(actor, other, pasta, 1.5m);
        await recipes.SaveAsync(actor, meal, sauce, 0.25m);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(actor, meal, pasta, 9m));
        Assert.Equal(0.5m, (await recipes.GetAsync(actor, meal)).Items.Single(x => x.IngredientId == pasta).Quantity);
        await recipes.SaveAsync(actor, meal, pasta, 0.1234m, editing: true);
        await ingredients.SaveAsync(actor, new() { UnitId = unit, Id = pasta, Name = "Wholemeal pasta" });
        await meals.SaveAsync(actor, new() { Id = meal, Name = "Weeknight bake" });
        var recipe = await recipes.GetAsync(actor, meal);
        Assert.Equal("Weeknight bake", recipe.Name);
        Assert.Equal("Wholemeal pasta", recipe.Items.Single(x => x.IngredientId == pasta).Name);
        Assert.Equal(0.1234m, recipe.Items.Single(x => x.IngredientId == pasta).Quantity);
        Assert.Equal(1.5m, (await recipes.GetAsync(actor, other)).Items.Single().Quantity);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.DeleteAsync(actor, pasta));
        await recipes.RemoveAsync(actor, meal, sauce);
        await ingredients.DeleteAsync(actor, sauce);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(actor, meal, sauce, 1m, editing: true));
        Assert.Equal(2, (await planner.GetAsync(actor)).Count);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => meals.DeleteAsync(actor, meal, false));
        await meals.DeleteAsync(actor, meal, true);
        Assert.Equal(other, (await planner.GetAsync(actor)).Single().MealId);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(other, (await db.RecipeItems.SingleAsync()).MealId);
        Assert.Single(await ingredients.ListAsync(actor));
        await recipes.RemoveAsync(actor, other, pasta);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(actor, other, pasta, 1m, editing: true));
        await ingredients.DeleteAsync(actor, pasta);
        Assert.Empty(await ingredients.ListAsync(actor));
        Assert.Single(await meals.ListAsync(actor));
    }

    [Fact]
    public async Task QuantitiesValidateRangePrecisionAndCultureWithoutChangingRecipe()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var owner = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var unit = await app.Services.GetRequiredService<ManageIngredientUnits>().CreateAsync(actor, new() { Name = "packets" });
        var meal = await app.Services.GetRequiredService<ManageMeals>().SaveAsync(actor, new() { Name = "Pasta" });
        var ingredient = await app.Services.GetRequiredService<ManageIngredients>().SaveAsync(actor, new() { UnitId = unit, Name = "Pasta" });
        var recipes = app.Services.GetRequiredService<ManageRecipe>();
        foreach (var invalid in new[] { 0m, -1m, 0.00001m, 100000000m })
            await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(actor, meal, ingredient, invalid));
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
            foreach (var invalid in new[] { "", "abc", "1,5", "1e2", "0.50001", "0.5000000000000000000000000000000001" })
                await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(actor, meal, ingredient, invalid));
            Assert.Empty((await recipes.GetAsync(actor, meal)).Items);
            await recipes.SaveAsync(actor, meal, ingredient, " 0.5 ");
            Assert.Equal(0.5m, (await recipes.GetAsync(actor, meal)).Items.Single().Quantity);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            await recipes.SaveAsync(actor, meal, ingredient, "1,5", editing: true);
            Assert.Equal(1.5m, (await recipes.GetAsync(actor, meal)).Items.Single().Quantity);
            await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(actor, meal, ingredient, "1.5", editing: true));
        }
        finally { CultureInfo.CurrentCulture = previous; }
        await recipes.SaveAsync(actor, meal, ingredient, ManageRecipe.MaximumQuantity, editing: true);
        Assert.Equal(ManageRecipe.MaximumQuantity, (await recipes.GetAsync(actor, meal)).Items.Single().Quantity);
    }

    [Fact]
    public async Task MembersCanManageRecipesButForeignAndRevokedAccessFails()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var register = app.Services.GetRequiredService<RegisterAccount>();
        var owner = await register.RunAsync(RegistrationTests.Request());
        var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
        var actor = MembershipTests.Actor(owner.UserId!);
        var unit = await app.Services.GetRequiredService<ManageIngredientUnits>().CreateAsync(actor, new() { Name = "packets" });
        var stranger = MembershipTests.Actor(foreign.UserId!);
        var foreignUnit = await app.Services.GetRequiredService<ManageIngredientUnits>().CreateAsync(stranger, new() { Name = "packets" });
        var input = RegistrationTests.Request("member@example.test"); input.Choice = HouseholdChoice.Join;
        input.InvitationCode = await app.Services.GetRequiredService<ManageInvitations>().CreateAsync(actor);
        var member = await register.RunAsync(input);
        var memberActor = MembershipTests.Actor(member.UserId!);
        var ingredients = app.Services.GetRequiredService<ManageIngredients>();
        var meals = app.Services.GetRequiredService<ManageMeals>();
        var recipes = app.Services.GetRequiredService<ManageRecipe>();
        var ingredient = await ingredients.SaveAsync(memberActor, new() { UnitId = unit, Name = "Pasta" });
        var meal = await meals.SaveAsync(actor, new() { Name = "Bake" });
        var otherIngredient = await ingredients.SaveAsync(stranger, new() { UnitId = foreignUnit, Name = "Pasta" });
        var otherMeal = await meals.SaveAsync(stranger, new() { Name = "Bake" });
        await recipes.SaveAsync(memberActor, meal, ingredient, 0.5m);
        Assert.Single((await recipes.GetAsync(memberActor, meal)).Items);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(stranger, new() { UnitId = foreignUnit, Id = ingredient, Name = "Stolen" }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.DeleteAsync(stranger, ingredient));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.GetAsync(stranger, meal));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(stranger, meal, otherIngredient, 1m));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(actor, meal, otherIngredient, 1m));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(actor, otherMeal, ingredient, 1m));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.RemoveAsync(stranger, meal, ingredient));
        await app.Services.GetRequiredService<ManageAccountMembers>().RemoveAsync(actor, member.UserId!);
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.ListAsync(memberActor));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.SaveAsync(memberActor, new() { UnitId = unit, Name = "Rice" }));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => ingredients.DeleteAsync(memberActor, ingredient));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.GetAsync(memberActor, meal));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.SaveAsync(memberActor, meal, ingredient, 2m, editing: true));
        await Assert.ThrowsAsync<HouseholdRuleException>(() => recipes.RemoveAsync(memberActor, meal, ingredient));
        Assert.Equal(0.5m, (await recipes.GetAsync(actor, meal)).Items.Single().Quantity);
    }

    [Fact]
    public async Task RecipeAndIngredientRoutesRenderAndOldCookiesLoseAccess()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        var owner = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(RegistrationTests.Request());
        var actor = MembershipTests.Actor(owner.UserId!);
        var unit = await app.Services.GetRequiredService<ManageIngredientUnits>().CreateAsync(actor, new() { Name = "packets" });
        using var browser = app.Browser();
        await BrowserForms.PostAsync(browser, "/Account/Register", "register", new()
        {
            ["Input.Email"] = "recipe-browser@example.test", ["Input.Name"] = "Member",
            ["Input.Password"] = "HomeHub!123", ["Input.ConfirmPassword"] = "HomeHub!123",
            ["Input.Choice"] = "Join", ["Input.InvitationCode"] = await app.Services.GetRequiredService<ManageInvitations>().CreateAsync(actor)
        });
        var meal = await app.Services.GetRequiredService<ManageMeals>().SaveAsync(actor, new() { Name = "Pasta bake" });
        var ingredient = await app.Services.GetRequiredService<ManageIngredients>().SaveAsync(actor, new() { UnitId = unit, Name = "Pasta" });
        await app.Services.GetRequiredService<ManageRecipe>().SaveAsync(actor, meal, ingredient, 0.5m);
        var recipePath = $"/Meals/{meal}/Recipe";
        foreach (var path in new[] { "/Ingredients", recipePath })
        {
            var response = await browser.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("<!--Blazor:{\"type\":\"server\"", html);
            Assert.Contains("disabled", html);
            Assert.Contains("Pasta", html);
        }
        Assert.Contains("Quantity: 0.5", await browser.GetStringAsync(recipePath));
        Assert.Contains("Recipe</a>", await browser.GetStringAsync("/Meals"));
        var accounts = app.Services.GetRequiredService<ManageAccountMembers>();
        var member = (await accounts.ListAsync(actor)).Single(x => !x.IsOwner);
        await accounts.RemoveAsync(actor, member.UserId);
        foreach (var path in new[] { "/Ingredients", recipePath })
        {
            var response = await browser.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Household/Onboarding", response.Headers.Location!.OriginalString);
        }
    }
}
