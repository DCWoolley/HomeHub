using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.HouseholdItems.ManageCatalogue;

public sealed class ManageCatalogue(IDbContextFactory<ApplicationDbContext> factory)
{
    public static IReadOnlyList<string> SuggestedUnits { get; } = Array.AsReadOnly(new[] { "rolls", "bottles", "tablets", "bags", "ml", "litres" });
    public sealed record Item(Guid Id, string Name, string Unit, decimal? ExpectedWeeklyUsage, Guid Version);
    public sealed class Request
    {
        public Guid? Id { get; set; }
        public Guid Version { get; set; }
        public string ExpectedWeeklyUsage { get; set; } = "";
        public string ReplacementWeeklyUsage { get; set; } = "";
        [Required(ErrorMessage = "Enter a household item name."), StringLength(100)] public string Name { get; set; } = "";
        [Required(ErrorMessage = "Enter a usage unit."), StringLength(50)] public string Unit { get; set; } = "";
    }

    public async Task<IReadOnlyList<Item>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        return await db.HouseholdItems.AsNoTracking().Where(x => x.HouseholdId == current.HouseholdId && !x.IsArchived)
            .OrderBy(x => x.Name).Select(x => new Item(x.Id, x.Name, x.Unit, x.ExpectedWeeklyUsage, x.Version)).ToListAsync(ct);
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
        var duplicate = await db.HouseholdItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.HouseholdId == current.HouseholdId && x.NormalizedName == normalized && x.Id != input.Id, ct);
        if (duplicate is not null)
            throw new HouseholdRuleException(duplicate.IsArchived
                ? "A household item with this name already exists. Restore it from Archived items."
                : "A household item with this name already exists. Edit it from the catalogue.");
        var item = input.Id is { } id
            ? await db.HouseholdItems.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == current.HouseholdId, ct)
                ?? throw new HouseholdRuleException("Household item not found. Refresh the catalogue.")
            : new HouseholdItem { HouseholdId = current.HouseholdId };
        if (input.Id is null) db.HouseholdItems.Add(item);
        else if (item.IsArchived || input.Version != item.Version)
            throw new HouseholdRuleException("This household item changed since you opened the editor. Cancel the edit, refresh the catalogue and reopen it before saving.");
        item.Name = name;
        item.NormalizedName = normalized;
        var changingBaselineUnit = item.ExpectedWeeklyUsage is not null && item.Unit != input.Unit.Trim();
        var usage = changingBaselineUnit
            ? ParseUsage(input.ReplacementWeeklyUsage) ?? throw new HouseholdRuleException("Enter an explicit replacement Expected Weekly Usage in the new unit before saving.")
            : ParseUsage(input.ExpectedWeeklyUsage);
        item.Unit = input.Unit.Trim();
        item.ExpectedWeeklyUsage = usage;
        item.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return item.Id;
    }

    public async Task ArchiveAsync(ClaimsPrincipal actor, Guid id, Guid version, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        var item = await db.HouseholdItems.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == current.HouseholdId, ct)
            ?? throw new HouseholdRuleException("Household item not found. Refresh the catalogue.");
        if (item.IsArchived) throw new HouseholdRuleException("This household item is already archived. Refresh the catalogue.");
        if (item.Version != version) throw new HouseholdRuleException("This household item changed since you loaded it. Refresh the catalogue before archiving.");
        item.IsArchived = true;
        item.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static decimal? ParseUsage(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var fractionStart = trimmed.IndexOf(separator, StringComparison.Ordinal);
        if ((fractionStart >= 0 && trimmed.Length - fractionStart - separator.Length > 4)
            || !decimal.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.CurrentCulture, out var parsed)
            || parsed <= 0 || parsed > 99999999.9999m)
            throw new HouseholdRuleException("Enter Expected Weekly Usage from 0.0001 to 99,999,999.9999 with at most four decimal places, using the example's decimal separator. Leave blank to clear it.");
        return parsed;
    }
}
