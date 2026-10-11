using System.Security.Cryptography;
using System.Text;
using HomeHub.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Households;

public enum HouseholdChoice { Create, Join }
public sealed class HouseholdRuleException(string message) : Exception(message);

public static class HouseholdEnrollment
{
    public static string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));

    public static async Task<Guid> ApplyAsync(ApplicationDbContext db, string userId, string name,
        HouseholdChoice choice, string? householdName, string? invitationCode, DateTime now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new HouseholdRuleException("Enter a member name of up to 100 characters.");
        if (await db.HouseholdMemberships.AnyAsync(x => x.UserId == userId, ct))
            throw new HouseholdRuleException("Your account already belongs to a household.");

        Guid householdId;
        if (choice == HouseholdChoice.Create)
        {
            if (string.IsNullOrWhiteSpace(householdName) || householdName.Trim().Length > 100)
                throw new HouseholdRuleException("Enter a household name of up to 100 characters.");
            var household = new Household { Name = householdName.Trim() };
            db.Households.Add(household);
            householdId = household.Id;
        }
        else if (choice == HouseholdChoice.Join)
        {
            if (string.IsNullOrWhiteSpace(invitationCode) || invitationCode.Length > 100)
                throw new HouseholdRuleException("Enter a valid invitation code.");
            var hash = HashCode(invitationCode);
            var invitation = await db.HouseholdInvitations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
            if (invitation is null)
                throw new HouseholdRuleException("The invitation code is invalid, expired, revoked, or already used.");

            // All household mutations take this lock before checking membership or changing access.
            await db.Households.Where(x => x.Id == invitation.HouseholdId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Version, x => x.Version + 1), ct);
            var redeemed = await db.HouseholdInvitations
                .Where(x => x.Id == invitation.Id && x.UsedAtUtc == null && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.UsedAtUtc, now), ct);
            if (redeemed != 1)
                throw new HouseholdRuleException("The invitation code is invalid, expired, revoked, or already used.");
            householdId = invitation.HouseholdId;
        }
        else throw new HouseholdRuleException("Choose to create or join a household.");

        var member = new HouseholdMember { HouseholdId = householdId, Name = name.Trim() };
        db.HouseholdMembers.Add(member);
        db.HouseholdMemberships.Add(new HouseholdMembership
        {
            UserId = userId, HouseholdId = householdId, MemberId = member.Id, IsOwner = choice == HouseholdChoice.Create
        });
        await db.SaveChangesAsync(ct);
        return householdId;
    }
}
