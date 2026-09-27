using System.Security.Cryptography;
using System.Text;
using HomeHub.Application.Features.Account.Register;
using HomeHub.Application.Features.Homes.JoinHome;
using HomeHub.Application.Features.Homes.ManageHome;
using HomeHub.Application.Features.Homes.ManageMembers;
using HomeHub.Domain.Homes;
using HomeHub.Infrastructure.Identity;
using HomeHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Infrastructure.Homes;

public sealed partial class HomeService(ApplicationDbContext database, UserManager<ApplicationUser> users, TimeProvider clock)
    : IHomeRegistration, IHomeAccess, IHomeMembers
{
    public async Task<RegistrationResult> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Home.Validate() is { } error) return new(null, [error]);
        if (request.External is null && string.IsNullOrEmpty(request.Password))
            return new(null, ["A password is required."]);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
            var result = request.External is null
                ? await users.CreateAsync(user, request.Password!)
                : await users.CreateAsync(user);
            if (!result.Succeeded) return new(null, result.Errors.Select(item => item.Description).ToArray());
            if (request.External is { } external)
            {
                result = await users.AddLoginAsync(user, new UserLoginInfo(external.Provider, external.Key, external.DisplayName));
                if (!result.Succeeded) return new(null, result.Errors.Select(item => item.Description).ToArray());
            }

            var membershipError = await AddMembershipAsync(user.Id, request.Home, cancellationToken);
            if (membershipError is not null) return new(null, [membershipError]);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(user.Id, []);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(null, ["This invitation has just been used. Ask the owner for a new code."]);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return new(null, ["This account or membership already exists. Sign in to continue."]);
        }
        finally
        {
            // Identity also saves through this context. Do not leave rolled-back users tracked.
            database.ChangeTracker.Clear();
        }
    }

    public async Task<HomeDetails?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var home = await (from membership in database.HomeMemberships
               join memberHome in database.Homes on membership.HomeId equals memberHome.Id
               where membership.UserId == userId
               select memberHome).AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (home is null) return null;
        var floors = await database.HomeFloors.AsNoTracking().Where(floor => floor.HomeId == home.Id)
            .OrderBy(floor => floor.Name).ThenBy(floor => floor.Id).ToListAsync(cancellationToken);
        var rooms = await (from room in database.HomeRooms.AsNoTracking()
                           join floor in database.HomeFloors on room.FloorId equals floor.Id
                           where floor.HomeId == home.Id
                           orderby room.Name, room.Id
                           select room).ToListAsync(cancellationToken);
        return new(home.Name, home.OwnerUserId == userId, floors.Select(floor =>
            new FloorDetails(floor.Id, floor.Name, rooms.Where(room => room.FloorId == floor.Id)
                .Select(room => new RoomDetails(room.Id, room.Name)).ToArray())).ToArray(), home.TimeZoneId);
    }

    public Task<string?> RenameAsync(string userId, string name, CancellationToken cancellationToken = default) =>
        ConfigureAsync(userId, name, (home, value) => home.Rename(userId, value), cancellationToken);

    public Task<string?> AddFloorAsync(string userId, string name, CancellationToken cancellationToken = default) =>
        ConfigureAsync(userId, name, (home, value) => database.HomeFloors.Add(home.AddFloor(userId, value)), cancellationToken);

    public async Task<string?> AddRoomAsync(string userId, Guid floorId, string name, CancellationToken cancellationToken = default)
    {
        var floor = await database.HomeFloors.AsNoTracking().SingleOrDefaultAsync(floor => floor.Id == floorId &&
            database.Homes.Any(home => home.Id == floor.HomeId && home.OwnerUserId == userId), cancellationToken);
        if (floor is null) return "Choose a floor in a home you own.";
        return await ConfigureAsync(userId, name,
            (home, value) => database.HomeRooms.Add(home.AddRoom(userId, floor, value)), cancellationToken);
    }

    private async Task<string?> ConfigureAsync(string userId, string name, Action<Home, string> configure, CancellationToken cancellationToken)
    {
        var home = await OwnedHomeAsync(userId, cancellationToken);
        if (home is null) return "Only the home owner can configure this home.";
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return "Enter a name of up to 100 characters.";
        configure(home, name);
        await database.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<string?> JoinAsync(string userId, HomeChoice choice, CancellationToken cancellationToken = default)
    {
        if (choice.Validate() is { } error) return error;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!await database.Users.AnyAsync(user => user.Id == userId, cancellationToken))
                return "Sign in to create or join a home.";
            if (await database.HomeMemberships.AnyAsync(member => member.UserId == userId, cancellationToken))
                return "You already belong to a home.";
            var membershipError = await AddMembershipAsync(userId, choice, cancellationToken);
            if (membershipError is not null) return membershipError;
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return "This invitation has just been used. Ask the owner for a new code.";
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return "You already belong to a home. Reload to view your membership.";
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<InvitationResult> InviteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var home = await OwnedHomeAsync(userId, cancellationToken);
        if (home is null) return new(null, null, "Only a home owner can generate invitations.");
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var invitation = home.Invite(userId, Hash(code), clock.GetUtcNow());
        database.HomeInvitations.Add(invitation);
        await database.SaveChangesAsync(cancellationToken);
        return new(code, invitation.ExpiresAt, null);
    }

    private async Task<string?> AddMembershipAsync(string userId, HomeChoice choice, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(choice.HomeName))
        {
            var home = new Home(choice.HomeName, userId);
            database.Homes.Add(home);
            await StoreMembershipAsync(new HomeMembership(home.Id, userId), cancellationToken);
            return null;
        }

        var hash = Hash(choice.InvitationCode!.Trim().ToUpperInvariant());
        var invitation = await database.HomeInvitations.SingleOrDefaultAsync(invite => invite.CodeHash == hash, cancellationToken);
        if (invitation is null || !invitation.IsAvailable(clock.GetUtcNow()))
            return "That invitation code is invalid, expired or already used, or has been revoked. Ask the home owner for a new code.";
        await StoreMembershipAsync(invitation.Redeem(userId, clock.GetUtcNow()), cancellationToken);
        return null;
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private Task<Home?> OwnedHomeAsync(string userId, CancellationToken cancellationToken) =>
        database.Homes.SingleOrDefaultAsync(home => home.OwnerUserId == userId &&
            database.HomeMemberships.Any(member => member.HomeId == home.Id && member.UserId == userId), cancellationToken);

    private async Task StoreMembershipAsync(HomeMembership membership, CancellationToken cancellationToken)
    {
        var name = await database.Users.Where(user => user.Id == membership.UserId)
            .Select(user => user.UserName).SingleAsync(cancellationToken) ?? "Member";
        database.HomeMembers.Add(new HomeMember(membership, name.Length > 100 ? name[..100] : name));
        database.HomeMemberships.Add(membership);
    }
}
