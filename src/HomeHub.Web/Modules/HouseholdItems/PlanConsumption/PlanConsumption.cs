using System.Globalization;
using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.HouseholdItems.PlanConsumption;

public sealed class PlanConsumption(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Item(string Name, string Unit, decimal WeeklyUsage, decimal Total);

    public async Task<IReadOnlyList<Item>> ListAsync(ClaimsPrincipal actor, string period, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        if (!int.TryParse(period, NumberStyles.None, CultureInfo.InvariantCulture, out var weeks) || weeks <= 0)
            throw new HouseholdRuleException("Enter a positive whole number of weeks from 1 to 2,147,483,647 (the supported numeric range).");
        var items = await db.HouseholdItems.AsNoTracking()
            .Where(x => x.HouseholdId == current.HouseholdId && !x.IsArchived && x.ExpectedWeeklyUsage != null)
            .OrderBy(x => x.Name).Select(x => new { x.Name, x.Unit, x.ExpectedWeeklyUsage }).ToListAsync(ct);
        return items.Select(x => new Item(x.Name, x.Unit, x.ExpectedWeeklyUsage!.Value, x.ExpectedWeeklyUsage.Value * weeks)).ToList();
    }
}
