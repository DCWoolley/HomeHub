using System.Security.Claims;
using System.Security.Cryptography;
using HomeHub.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Households.ManageInvitations;

public sealed class ManageInvitations(IDbContextFactory<ApplicationDbContext> factory, TimeProvider time)
{
    public sealed record Invitation(Guid Id, DateTime ExpiresAtUtc, DateTime? UsedAtUtc, DateTime? RevokedAtUtc);

    public async Task<IReadOnlyList<Invitation>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var member = await HouseholdAccess.RequireAsync(db, actor, owner: true, ct);
        return await db.HouseholdInvitations.Where(x => x.HouseholdId == member.HouseholdId)
            .OrderByDescending(x => x.ExpiresAtUtc)
            .Select(x => new Invitation(x.Id, x.ExpiresAtUtc, x.UsedAtUtc, x.RevokedAtUtc)).ToListAsync(ct);
    }

    public async Task<string> CreateAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var member = await HouseholdAccess.LockAsync(db, actor, owner: true, ct);
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        db.HouseholdInvitations.Add(new HouseholdInvitation
        {
            HouseholdId = member.HouseholdId, CodeHash = HouseholdEnrollment.HashCode(code),
            ExpiresAtUtc = time.GetUtcNow().UtcDateTime.AddDays(7)
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return code;
    }

    public async Task RevokeAsync(ClaimsPrincipal actor, Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var member = await HouseholdAccess.LockAsync(db, actor, owner: true, ct);
        var invitation = await db.HouseholdInvitations.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == member.HouseholdId, ct);
        if (invitation is null) throw new HouseholdRuleException("Invitation not found.");
        invitation.RevokedAtUtc ??= time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
