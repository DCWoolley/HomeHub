using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.HouseholdItems.ManageArchived;

public sealed class ManageArchived(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Item(Guid Id, string Name, string Unit, decimal? ExpectedWeeklyUsage, Guid Version);

    public async Task<IReadOnlyList<Item>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        return await db.HouseholdItems.AsNoTracking().Where(x => x.HouseholdId == current.HouseholdId && x.IsArchived)
            .OrderBy(x => x.Name).Select(x => new Item(x.Id, x.Name, x.Unit, x.ExpectedWeeklyUsage, x.Version)).ToListAsync(ct);
    }

    public async Task RestoreAsync(ClaimsPrincipal actor, Guid id, Guid version, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        var item = await db.HouseholdItems.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == current.HouseholdId, ct)
            ?? throw new HouseholdRuleException("Household item not found. Refresh archived items.");
        if (!item.IsArchived) throw new HouseholdRuleException("This household item is already restored. Refresh archived items.");
        if (item.Version != version) throw new HouseholdRuleException("This household item changed since you loaded it. Refresh archived items before restoring.");
        item.IsArchived = false;
        item.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
