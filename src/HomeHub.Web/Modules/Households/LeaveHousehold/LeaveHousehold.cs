using System.Security.Claims;
using HomeHub.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Households.LeaveHousehold;

public sealed class LeaveHousehold(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task RunAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        if (current.IsOwner) throw new HouseholdRuleException("Transfer ownership to another account member before leaving.");
        var membership = await db.HouseholdMemberships.SingleAsync(x => x.UserId == current.UserId, ct);
        var profile = await db.HouseholdMembers.SingleAsync(x => x.Id == membership.MemberId, ct);
        profile.IsRetired = true;
        db.HouseholdMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
