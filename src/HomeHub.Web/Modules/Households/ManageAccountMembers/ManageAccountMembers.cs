using System.Security.Claims;
using HomeHub.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Households.ManageAccountMembers;

public sealed class ManageAccountMembers(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Member(string UserId, string Name, bool IsOwner);
    public async Task<IReadOnlyList<Member>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        return await (from access in db.HouseholdMemberships
            join profile in db.HouseholdMembers on access.MemberId equals profile.Id
            where access.HouseholdId == current.HouseholdId
            orderby profile.Name
            select new Member(access.UserId, profile.Name, access.IsOwner)).ToListAsync(ct);
    }

    public async Task RemoveAsync(ClaimsPrincipal actor, string targetUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, owner: true, ct);
        var target = await db.HouseholdMemberships.SingleOrDefaultAsync(x => x.UserId == targetUserId && x.HouseholdId == current.HouseholdId, ct);
        if (target is null) throw new HouseholdRuleException("Account member not found.");
        if (target.IsOwner) throw new HouseholdRuleException("Transfer ownership before removing the owner.");
        var profile = await db.HouseholdMembers.SingleAsync(x => x.Id == target.MemberId && x.HouseholdId == current.HouseholdId, ct);
        profile.IsRetired = true;
        db.HouseholdMemberships.Remove(target);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
