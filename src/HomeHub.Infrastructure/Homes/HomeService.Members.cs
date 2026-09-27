using HomeHub.Application.Features.Homes.ManageMembers;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Infrastructure.Homes;

public sealed partial class HomeService
{
    public async Task<MembersDetails?> GetMembersAsync(string userId, CancellationToken cancellationToken = default)
    {
        var home = await (from membership in database.HomeMemberships
                          join item in database.Homes on membership.HomeId equals item.Id
                          where membership.UserId == userId
                          select item).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (home is null) return null;
        var members = await database.HomeMembers.AsNoTracking()
            .Where(member => member.HomeId == home.Id && !member.IsRetired &&
                (!member.IsAccountLinked || database.HomeMemberships.Any(link => link.MemberId == member.Id)))
            .OrderBy(member => member.DisplayName).ThenBy(member => member.Id)
            .Select(member => new MemberDetails(member.Id, member.DisplayName, member.Colour,
                database.HomeMemberships.Any(link => link.MemberId == member.Id && link.UserId == home.OwnerUserId),
                member.IsAccountLinked)).ToListAsync(cancellationToken);
        return new(home.OwnerUserId == userId, members);
    }

    public async Task<string?> CreateProfileAsync(string userId, string name, string? colour, CancellationToken cancellationToken = default)
    {
        var home = await OwnedHomeAsync(userId, cancellationToken);
        if (home is null) return "Only the home owner can manage members.";
        try { database.HomeMembers.Add(home.CreateProfile(userId, name, colour)); }
        catch (ArgumentException exception) { return exception.Message; }
        await database.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<string?> EditProfileAsync(string userId, Guid memberId, string name, string? colour, CancellationToken cancellationToken = default)
    {
        var home = await OwnedHomeAsync(userId, cancellationToken);
        if (home is null) return "Only the home owner can manage members.";
        var member = await database.HomeMembers.SingleOrDefaultAsync(member => member.Id == memberId &&
            member.HomeId == home.Id && !member.IsRetired && !member.IsAccountLinked, cancellationToken);
        if (member is null) return "Choose an active non-user member of your home.";
        try { home.EditProfile(userId, member, name, colour); }
        catch (ArgumentException exception) { return exception.Message; }
        await database.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<string?> RemoveMemberAsync(string userId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var home = await OwnedHomeAsync(userId, cancellationToken);
        if (home is null) return "Only the home owner can manage members.";
        var member = await database.HomeMembers.SingleOrDefaultAsync(member => member.Id == memberId &&
            member.HomeId == home.Id && !member.IsRetired, cancellationToken);
        if (member is null) return "Choose an active member of your home.";
        var membership = await database.HomeMemberships.SingleOrDefaultAsync(link => link.MemberId == member.Id, cancellationToken);
        try { home.RemoveMember(userId, member, membership); }
        catch (InvalidOperationException exception) { return exception.Message; }
        if (membership is not null) database.HomeMemberships.Remove(membership);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            return "This membership has changed. Reload the member list.";
        }
        return null;
    }
}
