using System.Security.Claims;
using HomeHub.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Households.TransferOwnership;

public sealed class TransferOwnership(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Candidate(string UserId, string Name);
    public async Task<IReadOnlyList<Candidate>> CandidatesAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, owner: true, ct);
        return await (from membership in db.HouseholdMemberships
            join profile in db.HouseholdMembers on membership.MemberId equals profile.Id
            where membership.HouseholdId == current.HouseholdId && !membership.IsOwner && !profile.IsRetired
            orderby profile.Name select new Candidate(membership.UserId, profile.Name)).ToListAsync(ct);
    }

    public async Task RunAsync(ClaimsPrincipal actor, string targetUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, owner: true, ct);
        if (current.UserId == targetUserId) throw new HouseholdRuleException("Choose another account member.");
        var target = await db.HouseholdMemberships.SingleOrDefaultAsync(x => x.UserId == targetUserId && x.HouseholdId == current.HouseholdId, ct);
        if (target is null || !await db.HouseholdMembers.AnyAsync(x => x.Id == target.MemberId && !x.IsRetired, ct))
            throw new HouseholdRuleException("Choose an active account member of your household.");
        var previous = await db.HouseholdMemberships.SingleAsync(x => x.UserId == current.UserId, ct);
        previous.IsOwner = false;
        // Save in two steps within one transaction to respect the filtered unique owner index.
        await db.SaveChangesAsync(ct);
        target.IsOwner = true;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
