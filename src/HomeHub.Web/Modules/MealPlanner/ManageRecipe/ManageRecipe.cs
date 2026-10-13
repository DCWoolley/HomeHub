using System.Globalization;
using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.MealPlanner.ManageRecipe;

public sealed class ManageRecipe(IDbContextFactory<ApplicationDbContext> factory)
{
    public const decimal MaximumQuantity = 99999999.9999m;
    public sealed record Item(Guid IngredientId, string Name, decimal Quantity);
    public sealed record Recipe(Guid MealId, string Name, IReadOnlyList<Item> Items);

    public Task SaveAsync(ClaimsPrincipal actor, Guid mealId, Guid ingredientId, string quantity,
        bool editing = false, CancellationToken ct = default)
    {
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var trimmed = quantity?.Trim() ?? "";
        var fractionStart = trimmed.IndexOf(separator, StringComparison.Ordinal);
        if (fractionStart >= 0 && trimmed.Length - fractionStart - separator.Length > 4)
            throw new HouseholdRuleException("Use at most four decimal places for the quantity.");
        // Group separators are not accepted: a fractional entry must never become a different whole quantity.
        if (!decimal.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.CurrentCulture, out var parsed))
            throw new HouseholdRuleException("Enter a numeric quantity using the decimal separator shown in the example.");
        return SaveAsync(actor, mealId, ingredientId, parsed, editing, ct);
    }

    public async Task<Recipe> GetAsync(ClaimsPrincipal actor, Guid mealId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        var meal = await ManageMeals.ManageMeals.FindAsync(db, current.HouseholdId, mealId, ct);
        var items = await db.RecipeItems.Where(x => x.HouseholdId == current.HouseholdId && x.MealId == mealId)
            .OrderBy(x => x.Ingredient.Name).Select(x => new Item(x.IngredientId, x.Ingredient.Name, x.Quantity)).ToListAsync(ct);
        return new(meal.Id, meal.Name, items);
    }

    public async Task SaveAsync(ClaimsPrincipal actor, Guid mealId, Guid ingredientId, decimal quantity,
        bool editing = false, CancellationToken ct = default)
    {
        if (quantity <= 0 || quantity > MaximumQuantity || decimal.Round(quantity, 4) != quantity)
            throw new HouseholdRuleException("Enter a quantity greater than zero, up to 99,999,999.9999, with at most four decimal places.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        await ManageMeals.ManageMeals.FindAsync(db, current.HouseholdId, mealId, ct);
        await ManageIngredients.ManageIngredients.FindAsync(db, current.HouseholdId, ingredientId, ct);
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
