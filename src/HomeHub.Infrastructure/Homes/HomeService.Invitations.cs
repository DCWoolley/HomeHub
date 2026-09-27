using HomeHub.Application.Features.Homes.ManageHome;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Infrastructure.Homes;

public sealed partial class HomeService
{
    public async Task<string?> ChangeTimeZoneAsync(string userId, string timeZoneId, CancellationToken cancellationToken = default)
    {
        var home = await OwnedHomeAsync(userId, cancellationToken);
        if (home is null) return "Only the home owner can configure this home.";
        try { home.ChangeTimeZone(userId, timeZoneId); }
        catch (ArgumentException) { return "Choose a valid timezone."; }
        await database.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<IReadOnlyList<InvitationDetails>> GetInvitationsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var home = await OwnedHomeAsync(userId, cancellationToken);
        if (home is null) return [];
        var invitations = await database.HomeInvitations.AsNoTracking().Where(invite => invite.HomeId == home.Id)
            .ToListAsync(cancellationToken);
        return invitations.OrderByDescending(invite => invite.ExpiresAt)
            .Select(invite => new InvitationDetails(invite.Id, invite.ExpiresAt, invite.Used, invite.Revoked)).ToArray();
    }

    public async Task<string?> RevokeInvitationAsync(string userId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        var home = await OwnedHomeAsync(userId, cancellationToken);
        if (home is null) return "Only the home owner can revoke invitations.";
        var invitation = await database.HomeInvitations.SingleOrDefaultAsync(invite =>
            invite.Id == invitationId && invite.HomeId == home.Id, cancellationToken);
        if (invitation is null) return "Choose an invitation for your home.";
        home.RevokeInvitation(userId, invitation);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            return "This invitation has changed. Reload the invitation list.";
        }
        return null;
    }
}
