using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HomeHub.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Households.ManageProfiles;

public sealed class ManageProfiles(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Member(Guid Id, string Name, bool HasAccount);
    public sealed class Request
    {
        public Guid? Id { get; set; }
        [Required, StringLength(100)] public string Name { get; set; } = "";
    }

    public async Task<IReadOnlyList<Member>> GetActiveMembersAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        return await db.HouseholdMembers.Where(x => x.HouseholdId == current.HouseholdId && !x.IsRetired)
            .OrderBy(x => x.Name).Select(x => new Member(x.Id, x.Name,
                db.HouseholdMemberships.Any(link => link.MemberId == x.Id))).ToListAsync(ct);
    }

    public async Task SaveAsync(ClaimsPrincipal actor, Request input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100)
            throw new HouseholdRuleException("Enter a member name of up to 100 characters.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        HouseholdMember profile;
        if (input.Id is null)
        {
            profile = new HouseholdMember { HouseholdId = current.HouseholdId };
            db.HouseholdMembers.Add(profile);
        }
        else profile = await EditableProfileAsync(db, current.HouseholdId, input.Id.Value, ct);
        profile.Name = input.Name.Trim();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RetireAsync(ClaimsPrincipal actor, Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        var profile = await EditableProfileAsync(db, current.HouseholdId, id, ct);
        profile.IsRetired = true;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task<HouseholdMember> EditableProfileAsync(ApplicationDbContext db, Guid householdId, Guid id, CancellationToken ct)
    {
        var profile = await db.HouseholdMembers.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId && !x.IsRetired, ct);
        if (profile is null) throw new HouseholdRuleException("Member profile not found.");
        if (await db.HouseholdMemberships.AnyAsync(x => x.MemberId == id, ct))
            throw new HouseholdRuleException("Manage account access through Account members.");
        return profile;
    }
}
