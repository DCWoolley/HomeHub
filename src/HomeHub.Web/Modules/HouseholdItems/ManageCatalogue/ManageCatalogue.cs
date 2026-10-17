using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.HouseholdItems.ManageCatalogue;

public sealed class ManageCatalogue(IDbContextFactory<ApplicationDbContext> factory)
{
    public static IReadOnlyList<string> SuggestedUnits { get; } = Array.AsReadOnly(new[] { "rolls", "bottles", "tablets", "bags", "ml", "litres" });
    public sealed record Item(Guid Id, string Name, string Unit);
    public sealed class Request
    {
        public Guid? Id { get; set; }
        [Required(ErrorMessage = "Enter a household item name."), StringLength(100)] public string Name { get; set; } = "";
        [Required(ErrorMessage = "Enter a usage unit."), StringLength(50)] public string Unit { get; set; } = "";
    }

    public async Task<IReadOnlyList<Item>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        return await db.HouseholdItems.AsNoTracking().Where(x => x.HouseholdId == current.HouseholdId)
            .OrderBy(x => x.Name).Select(x => new Item(x.Id, x.Name, x.Unit)).ToListAsync(ct);
    }

    public async Task<Guid> SaveAsync(ClaimsPrincipal actor, Request input, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100)
            throw new HouseholdRuleException("Enter a household item name of up to 100 characters.");
        if (string.IsNullOrWhiteSpace(input.Unit) || input.Unit.Trim().Length > 50)
            throw new HouseholdRuleException("Enter a usage unit of up to 50 characters.");
        var name = input.Name.Trim();
        var normalized = name.ToUpperInvariant();
        if (await db.HouseholdItems.AnyAsync(x => x.HouseholdId == current.HouseholdId && x.NormalizedName == normalized && x.Id != input.Id, ct))
            throw new HouseholdRuleException("A household item with this name already exists. Edit it from the catalogue.");
        var item = input.Id is { } id
            ? await db.HouseholdItems.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == current.HouseholdId, ct)
                ?? throw new HouseholdRuleException("Household item not found. Refresh the catalogue.")
            : new HouseholdItem { HouseholdId = current.HouseholdId };
        if (input.Id is null) db.HouseholdItems.Add(item);
        item.Name = name;
        item.NormalizedName = normalized;
        item.Unit = input.Unit.Trim();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return item.Id;
    }
}
