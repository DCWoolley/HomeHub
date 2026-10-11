using System.Security.Claims;
using HomeHub.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Households.GetHousehold;

public sealed class GetHousehold(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Response(Guid Id, string Name, string MemberName, bool IsOwner, int ActiveMembers);
    public async Task<Response?> RunAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var id = HouseholdAccess.UserId(actor);
        await using var db = await factory.CreateDbContextAsync(ct);
        return await (from access in db.HouseholdMemberships
            join home in db.Households on access.HouseholdId equals home.Id
            join member in db.HouseholdMembers on access.MemberId equals member.Id
            where access.UserId == id && !member.IsRetired
            select new Response(home.Id, home.Name, member.Name, access.IsOwner,
                db.HouseholdMembers.Count(x => x.HouseholdId == home.Id && !x.IsRetired))).SingleOrDefaultAsync(ct);
    }
}
