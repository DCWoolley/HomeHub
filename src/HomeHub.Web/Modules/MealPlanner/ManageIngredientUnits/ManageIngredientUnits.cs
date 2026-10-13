using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.MealPlanner.ManageIngredientUnits;

public sealed class ManageIngredientUnits(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Item(Guid Id, string Name);
    public sealed class Request
    {
        [Required, StringLength(50)] public string Name { get; set; } = "";
    }

    public async Task<IReadOnlyList<Item>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        return await db.IngredientUnits.Where(x => x.HouseholdId == current.HouseholdId).OrderBy(x => x.Name)
            .Select(x => new Item(x.Id, x.Name)).ToListAsync(ct);
    }

    public async Task<Guid> CreateAsync(ClaimsPrincipal actor, Request input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 50)
            throw new HouseholdRuleException("Enter a unit name of up to 50 characters.");
        var name = input.Name.Trim();
        var normalized = name.ToUpperInvariant();
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        var unit = await db.IngredientUnits.SingleOrDefaultAsync(x => x.HouseholdId == current.HouseholdId && x.NormalizedName == normalized, ct);
        if (unit is null)
        {
            unit = new() { HouseholdId = current.HouseholdId, Name = name, NormalizedName = normalized };
            db.IngredientUnits.Add(unit);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return unit.Id;
    }
}
