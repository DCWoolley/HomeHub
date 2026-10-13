using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.MealPlanner.ManageIngredients;

public sealed class ManageIngredients(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Item(Guid Id, string Name, Guid UnitId, string Unit, decimal Amount);
    public sealed class Request
    {
        public Guid? Id { get; set; }
        [Required, StringLength(100)] public string Name { get; set; } = "";
        [Required(ErrorMessage = "Choose a unit.")] public Guid? UnitId { get; set; }
        [Required] public string Amount { get; set; } = "1";
    }

    public async Task<IReadOnlyList<Item>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        return await db.Ingredients.Where(x => x.HouseholdId == current.HouseholdId).OrderBy(x => x.Name)
            .Select(x => new Item(x.Id, x.Name, x.UnitId, x.Unit.Name, x.Amount)).ToListAsync(ct);
    }

    public async Task<Guid> SaveAsync(ClaimsPrincipal actor, Request input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100)
            throw new HouseholdRuleException("Enter an ingredient name of up to 100 characters.");
        var name = input.Name.Trim();
        var normalized = name.ToUpperInvariant();
        var amount = IngredientQuantity.Parse(input.Amount, "amount");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        if (input.UnitId is not { } unitId || !await db.IngredientUnits.AnyAsync(x => x.Id == unitId && x.HouseholdId == current.HouseholdId, ct))
            throw new HouseholdRuleException("Choose a unit from your household's unit list.");
        if (await db.Ingredients.AnyAsync(x => x.HouseholdId == current.HouseholdId && x.NormalizedName == normalized && x.Id != input.Id, ct))
            throw new HouseholdRuleException("An ingredient with this name already exists. Choose it from the ingredient list.");
        var ingredient = input.Id is { } id ? await FindAsync(db, current.HouseholdId, id, ct)
            : new Ingredient { HouseholdId = current.HouseholdId };
        if (input.Id is null) db.Ingredients.Add(ingredient);
        if (input.Id is not null && ingredient.UnitId != unitId
            && await db.RecipeItems.AnyAsync(x => x.HouseholdId == current.HouseholdId && x.IngredientId == ingredient.Id, ct))
            throw new HouseholdRuleException("This ingredient is used in recipes. Remove it from those recipes before changing its unit.");
        ingredient.Name = name;
        ingredient.NormalizedName = normalized;
        ingredient.UnitId = unitId;
        ingredient.Amount = amount;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ingredient.Id;
    }

    public async Task DeleteAsync(ClaimsPrincipal actor, Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        var ingredient = await FindAsync(db, current.HouseholdId, id, ct);
        if (await db.RecipeItems.AnyAsync(x => x.HouseholdId == current.HouseholdId && x.IngredientId == id, ct))
            throw new HouseholdRuleException("This ingredient is used in a recipe. Remove it from all recipes before deleting it.");
        db.Ingredients.Remove(ingredient);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    internal static async Task<Ingredient> FindAsync(ApplicationDbContext db, Guid householdId, Guid id, CancellationToken ct) =>
        await db.Ingredients.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, ct)
        ?? throw new HouseholdRuleException("Ingredient not found. Refresh the ingredient list.");
}
