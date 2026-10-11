using System.Security.Claims;
using HomeHub.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Households;

public static class HouseholdAccess
{
    public static string UserId(ClaimsPrincipal actor)
    {
        if (actor.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(actor.FindFirstValue(ClaimTypes.NameIdentifier)))
            throw new HouseholdRuleException("Please sign in.");
        return actor.FindFirstValue(ClaimTypes.NameIdentifier)!;
    }

    public static async Task<HouseholdMembership> RequireAsync(ApplicationDbContext db, ClaimsPrincipal actor,
        bool owner = false, CancellationToken ct = default)
    {
        var userId = UserId(actor);
        var member = await db.HouseholdMemberships.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (member is null) throw new HouseholdRuleException("Create or join a household to continue.");
        if (owner && !member.IsOwner) throw new HouseholdRuleException("Only the household owner can do this.");
        return member;
    }

    public static async Task<HouseholdMembership> LockAsync(ApplicationDbContext db, ClaimsPrincipal actor,
        bool owner = false, CancellationToken ct = default)
    {
        var member = await RequireAsync(db, actor, ct: ct);
        // A database write lock serializes joining, removal, leaving, and ownership transfer.
        await db.Households.Where(x => x.Id == member.HouseholdId)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Version, x => x.Version + 1), ct);
        var refreshed = await RequireAsync(db, actor, owner, ct);
        if (refreshed.HouseholdId != member.HouseholdId) throw new HouseholdRuleException("Your household changed. Please try again.");
        return refreshed;
    }
}
