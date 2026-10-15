using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.MealPlanner.ManageRecipe;

public sealed class ManageRecipe(IDbContextFactory<ApplicationDbContext> factory)
{
    public const decimal MaximumQuantity = IngredientQuantity.Maximum;
    public sealed record Item(Guid IngredientId, string Name, decimal Quantity, string Unit, Guid UnitId = default);
    public sealed record Recipe(Guid MealId, string Name, IReadOnlyList<Item> Items);

    public Task SaveAsync(ClaimsPrincipal actor, Guid mealId, Guid ingredientId, string quantity,
        bool editing = false, Guid? expectedUnitId = null, CancellationToken ct = default)
    {
        return SaveAsync(actor, mealId, ingredientId, IngredientQuantity.Parse(quantity, "quantity"), editing, expectedUnitId, ct);
    }

    public async Task<Recipe> GetAsync(ClaimsPrincipal actor, Guid mealId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        var meal = await ManageMeals.ManageMeals.FindAsync(db, current.HouseholdId, mealId, ct);
        var items = await db.RecipeItems.Where(x => x.HouseholdId == current.HouseholdId && x.MealId == mealId)
            .OrderBy(x => x.Ingredient.Name).Select(x => new Item(x.IngredientId, x.Ingredient.Name, x.Quantity, x.Ingredient.Unit.Name, x.Ingredient.UnitId)).ToListAsync(ct);
        return new(meal.Id, meal.Name, items);
    }

    public async Task SaveAsync(ClaimsPrincipal actor, Guid mealId, Guid ingredientId, decimal quantity,
        bool editing = false, Guid? expectedUnitId = null, CancellationToken ct = default)
    {
        IngredientQuantity.Validate(quantity, "quantity");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        await ManageMeals.ManageMeals.FindAsync(db, current.HouseholdId, mealId, ct);
        var ingredient = await ManageIngredients.ManageIngredients.FindAsync(db, current.HouseholdId, ingredientId, ct);
        if (expectedUnitId is { } expected && ingredient.UnitId != expected)
            throw new HouseholdRuleException("The ingredient's unit changed. Refresh the recipe before entering a quantity.");
        var item = await db.RecipeItems.SingleOrDefaultAsync(x => x.HouseholdId == current.HouseholdId
            && x.MealId == mealId && x.IngredientId == ingredientId, ct);
        if (editing && item is null)
            throw new HouseholdRuleException("This recipe item was removed. Refresh the recipe before adding it again.");
        if (!editing && item is not null)
            throw new HouseholdRuleException("This ingredient is already in the recipe. Edit its existing quantity instead.");
        if (item is null)
        {
            item = new RecipeItem { HouseholdId = current.HouseholdId, MealId = mealId, IngredientId = ingredientId };
            db.RecipeItems.Add(item);
        }
        item.Quantity = quantity;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RemoveAsync(ClaimsPrincipal actor, Guid mealId, Guid ingredientId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        await ManageMeals.ManageMeals.FindAsync(db, current.HouseholdId, mealId, ct);
        var item = await db.RecipeItems.SingleOrDefaultAsync(x => x.HouseholdId == current.HouseholdId
            && x.MealId == mealId && x.IngredientId == ingredientId, ct)
            ?? throw new HouseholdRuleException("Recipe item not found. Refresh the recipe.");
        db.RecipeItems.Remove(item);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
