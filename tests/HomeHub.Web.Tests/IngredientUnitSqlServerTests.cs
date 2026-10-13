using HomeHub.Web.Data;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.MealPlanner;
using HomeHub.Web.Modules.MealPlanner.ManageIngredients;
using HomeHub.Web.Modules.MealPlanner.ManageIngredientUnits;
using HomeHub.Web.Modules.MealPlanner.ManageMeals;
using HomeHub.Web.Modules.MealPlanner.ManageRecipe;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class IngredientUnitSqlServerTests
{
    [LocalDbFact]
    public async Task MigrationPreservesPacketsAndConcurrentUnitChangesCannotReinterpretRecipes()
    {
        var name = "HomeHub_RestartTest_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;MultipleActiveResultSets=True";
        try
        {
            using var app = new RestartFactory(connection);
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.GetService<IMigrator>().MigrateAsync("20261013172140_MealRecipes");
            var register = app.Services.GetRequiredService<RegisterAccount>();
            var owner = await register.RunAsync(RegistrationTests.Request());
            var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
            var actor = MembershipTests.Actor(owner.UserId!);
            var stranger = MembershipTests.Actor(foreign.UserId!);
            var meal = await app.Services.GetRequiredService<ManageMeals>().SaveAsync(actor, new() { Name = "Bake" });
            var household = (await db.HouseholdMemberships.SingleAsync(x => x.UserId == owner.UserId)).HouseholdId;
            var pasta = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Ingredients (Id, HouseholdId, Name, NormalizedName) VALUES ({pasta}, {household}, N'Pasta', N'PASTA');
                INSERT INTO RecipeItems (HouseholdId, MealId, IngredientId, Quantity) VALUES ({household}, {meal}, {pasta}, 0.5);
                """);
            await db.Database.MigrateAsync();
            var units = app.Services.GetRequiredService<ManageIngredientUnits>();
            var ingredients = app.Services.GetRequiredService<ManageIngredients>();
            var recipes = app.Services.GetRequiredService<ManageRecipe>();
            var migrated = (await ingredients.ListAsync(actor)).Single();
            Assert.Equal(pasta, migrated.Id); Assert.Equal(1m, migrated.Amount); Assert.Equal("packets", migrated.Unit);
            Assert.Equal(0.5m, (await recipes.GetAsync(actor, meal)).Items.Single().Quantity);
            var concurrent = await Task.WhenAll(units.CreateAsync(actor, new() { Name = "grams" }), units.CreateAsync(actor, new() { Name = " GRAMS " }));
            Assert.Equal(concurrent[0], concurrent[1]);
            Assert.Equal(2, (await units.ListAsync(actor)).Count);
            var grams = concurrent[0];
            var foreignUnit = await units.CreateAsync(stranger, new() { Name = "grams" });
            async Task InvalidIngredient(Guid unitId, decimal amount)
            {
                await using var invalidScope = app.Services.CreateAsyncScope();
                var invalidDb = invalidScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                invalidDb.Ingredients.Add(new Ingredient { HouseholdId = household, Name = "Invalid", NormalizedName = "INVALID", UnitId = unitId, Amount = amount });
                await Assert.ThrowsAsync<DbUpdateException>(() => invalidDb.SaveChangesAsync());
            }
            await InvalidIngredient(foreignUnit, 1m);
            await InvalidIngredient(grams, 0m);
            await InvalidIngredient(grams, -1m);
            var rice = await ingredients.SaveAsync(actor, new() { Name = "Rice", UnitId = grams, Amount = "500.1234" });
            Assert.Equal(500.1234m, (await ingredients.ListAsync(actor)).Single(x => x.Id == rice).Amount);
            async Task<bool> Attempt(Func<Task> action)
            {
                try { await action(); return true; } catch (HouseholdRuleException) { return false; }
            }
            var race = await Task.WhenAll(
                Attempt(() => ingredients.SaveAsync(actor, new() { Id = rice, Name = "Rice", UnitId = migrated.UnitId, Amount = "1" })),
                Attempt(() => recipes.SaveAsync(actor, meal, rice, 250m, expectedUnitId: grams)));
            Assert.Single(race, x => x);
            var riceNow = (await ingredients.ListAsync(actor)).Single(x => x.Id == rice);
            var riceItem = (await recipes.GetAsync(actor, meal)).Items.SingleOrDefault(x => x.IngredientId == rice);
            if (riceItem is not null) { Assert.Equal(grams, riceNow.UnitId); Assert.Equal(250m, riceItem.Quantity); }
            else Assert.Equal(migrated.UnitId, riceNow.UnitId);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
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
