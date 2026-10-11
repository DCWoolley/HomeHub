using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.MealPlanner.ManageMeals;

public sealed class ManageMeals(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Item(Guid Id, string Name);
    public sealed record Deletion(Guid Id, string Name, int PlannedOccurrences);
    public sealed class Request
    {
        public Guid? Id { get; set; }
        [Required, StringLength(100)] public string Name { get; set; } = "";
    }

    public async Task<IReadOnlyList<Item>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        return await db.Meals.Where(x => x.HouseholdId == current.HouseholdId).OrderBy(x => x.Name)
            .Select(x => new Item(x.Id, x.Name)).ToListAsync(ct);
    }

    public async Task<Guid> SaveAsync(ClaimsPrincipal actor, Request input, CancellationToken ct = default)
    {
        var name = ValidateName(input.Name);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        var normalized = name.ToUpperInvariant();
        if (await db.Meals.AnyAsync(x => x.HouseholdId == current.HouseholdId && x.NormalizedName == normalized && x.Id != input.Id, ct))
            throw new HouseholdRuleException("A meal with this name already exists. Choose it from the meal list.");
        var meal = input.Id is { } id ? await FindAsync(db, current.HouseholdId, id, ct)
            : new Meal { HouseholdId = current.HouseholdId };
        if (input.Id is null) db.Meals.Add(meal);
        meal.Name = name;
        meal.NormalizedName = normalized;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return meal.Id;
    }

    public async Task<Deletion> PreviewDeleteAsync(ClaimsPrincipal actor, Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        var meal = await FindAsync(db, current.HouseholdId, id, ct);
        return new(id, meal.Name, await db.PlannedMeals.CountAsync(x => x.HouseholdId == current.HouseholdId && x.MealId == id, ct));
    }

    public async Task DeleteAsync(ClaimsPrincipal actor, Guid id, bool removeFromPlans, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        var meal = await FindAsync(db, current.HouseholdId, id, ct);
        if (!removeFromPlans && await db.PlannedMeals.AnyAsync(x => x.HouseholdId == current.HouseholdId && x.MealId == id, ct))
            throw new HouseholdRuleException("This meal is now in use. Review the warning and confirm deletion again.");
        db.Meals.Remove(meal);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    internal static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new HouseholdRuleException("Enter a meal name of up to 100 characters.");
        return name.Trim();
    }

    internal static async Task<Meal> FindAsync(ApplicationDbContext db, Guid householdId, Guid id, CancellationToken ct) =>
        await db.Meals.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, ct)
        ?? throw new HouseholdRuleException("Meal not found. Refresh the meal list.");
}
