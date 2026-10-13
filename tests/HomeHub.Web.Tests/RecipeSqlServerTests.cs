using HomeHub.Web.Data;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.MealPlanner;
using HomeHub.Web.Modules.MealPlanner.ManageIngredients;
using HomeHub.Web.Modules.MealPlanner.ManageMeals;
using HomeHub.Web.Modules.MealPlanner.ManageRecipe;
using HomeHub.Web.Modules.MealPlanner.PlanWeek;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class RecipeSqlServerTests
{
    [LocalDbFact]
    public async Task AdditiveMigrationDecimalsConstraintsAndConcurrentWritesPreserveRecipes()
    {
        var name = "HomeHub_RestartTest_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;MultipleActiveResultSets=True";
        try
        {
            using var app = new RestartFactory(connection);
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.GetService<IMigrator>().MigrateAsync("20261012183317_MealPlannerSlotSettings");
            var register = app.Services.GetRequiredService<RegisterAccount>();
            var owner = await register.RunAsync(RegistrationTests.Request());
            var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
            var actor = MembershipTests.Actor(owner.UserId!);
            var stranger = MembershipTests.Actor(foreign.UserId!);
            var meals = app.Services.GetRequiredService<ManageMeals>();
            var meal = await meals.SaveAsync(actor, new() { Name = "Bake" });
            var foreignMeal = await meals.SaveAsync(stranger, new() { Name = "Bake" });
            var planner = app.Services.GetRequiredService<PlanWeek>();
            await planner.SetAsync(actor, MealDay.Monday, MealTime.Dinner, meal);
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(meal, (await meals.ListAsync(actor)).Single().Id);
            Assert.Equal(meal, (await planner.GetAsync(actor)).Single().MealId);
            var ingredients = app.Services.GetRequiredService<ManageIngredients>();
            var recipes = app.Services.GetRequiredService<ManageRecipe>();
            Assert.Empty((await recipes.GetAsync(actor, meal)).Items);
            async Task<bool> Attempt(Func<Task> action)
            {
                try { await action(); return true; }
                catch (HouseholdRuleException) { return false; }
            }
            var creates = await Task.WhenAll(
                Attempt(async () => { await ingredients.SaveAsync(actor, new() { Name = "Pasta" }); }),
                Attempt(async () => { await ingredients.SaveAsync(actor, new() { Name = " pasta " }); }));
            Assert.Single(creates, x => x);
            var pasta = (await ingredients.ListAsync(actor)).Single().Id;
            var adds = await Task.WhenAll(
                Attempt(() => recipes.SaveAsync(actor, meal, pasta, 0.5m)),
                Attempt(() => recipes.SaveAsync(actor, meal, pasta, 0.5m)));
            Assert.Single(adds, x => x);
            Assert.Equal(0.5m, (await recipes.GetAsync(actor, meal)).Items.Single().Quantity);
            foreach (var quantity in new[] { 0.25m, 1m, 1.5m, 0.1234m, ManageRecipe.MaximumQuantity })
            {
                await recipes.SaveAsync(actor, meal, pasta, quantity, editing: true);
                Assert.Equal(quantity, (await recipes.GetAsync(actor, meal)).Items.Single().Quantity);
            }
            var household = (await db.HouseholdMemberships.SingleAsync(x => x.UserId == owner.UserId)).HouseholdId;
            var foreignIngredient = await ingredients.SaveAsync(stranger, new() { Name = "Pasta" });
            async Task InvalidRow(Guid targetMeal, Guid targetIngredient, decimal quantity)
            {
                await using var invalidScope = app.Services.CreateAsyncScope();
                var invalidDb = invalidScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                invalidDb.RecipeItems.Add(new() { HouseholdId = household, MealId = targetMeal, IngredientId = targetIngredient, Quantity = quantity });
                await Assert.ThrowsAsync<DbUpdateException>(() => invalidDb.SaveChangesAsync());
            }
            await InvalidRow(meal, pasta, 1m); // Unique ingredient per recipe.
            await InvalidRow(foreignMeal, pasta, 1m); // Composite meal FK.
            await InvalidRow(meal, foreignIngredient, 1m); // Composite ingredient FK.
            var unused = await ingredients.SaveAsync(actor, new() { Name = "Salt" });
            await InvalidRow(meal, unused, 0m); // Quantity check constraint.
            await InvalidRow(meal, unused, -1m);
            var racing = await ingredients.SaveAsync(actor, new() { Name = "Sauce" });
            var race = await Task.WhenAll(
                Attempt(() => recipes.SaveAsync(actor, meal, racing, 0.25m)),
                Attempt(() => ingredients.DeleteAsync(actor, racing)));
            Assert.Single(race, x => x);
            var remaining = await ingredients.ListAsync(actor);
            var items = (await recipes.GetAsync(actor, meal)).Items;
            Assert.Equal(remaining.Any(x => x.Id == racing), items.Any(x => x.IngredientId == racing));
            await meals.DeleteAsync(actor, meal, true);
            Assert.Empty(await db.RecipeItems.AsNoTracking().ToListAsync());
            Assert.Empty(await planner.GetAsync(actor));
            Assert.Contains(await ingredients.ListAsync(actor), x => x.Id == pasta);
        }
        finally
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
            if (db.Database.GetDbConnection().Database != name || !name.StartsWith("HomeHub_RestartTest_"))
                throw new InvalidOperationException("Unsafe test database cleanup.");
            await db.Database.EnsureDeletedAsync();
        }
    }
}
