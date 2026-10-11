using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HomeHub.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Households.Onboarding;

public sealed class Onboarding(IDbContextFactory<ApplicationDbContext> factory, TimeProvider time)
{
    public sealed class Request
    {
        [Required, StringLength(100)] public string Name { get; set; } = "";
        public HouseholdChoice Choice { get; set; }
        [StringLength(100)] public string? HouseholdName { get; set; }
        [StringLength(100)] public string? InvitationCode { get; set; }
    }

    public async Task RunAsync(ClaimsPrincipal actor, Request input, CancellationToken ct = default)
    {
        var id = HouseholdAccess.UserId(actor);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Users.AnyAsync(x => x.Id == id, ct))
            throw new HouseholdRuleException("Your account is no longer available.");
        try
        {
            await HouseholdEnrollment.ApplyAsync(db, id, input.Name, input.Choice, input.HouseholdName,
                input.InvitationCode, time.GetUtcNow().UtcDateTime, ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException) { throw new HouseholdRuleException("Joining could not be completed. Check your membership and invitation, then try again."); }
    }
}
