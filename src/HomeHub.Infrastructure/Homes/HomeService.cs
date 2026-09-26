using System.Security.Cryptography;
using System.Text;
using HomeHub.Application.Features.Account.Register;
using HomeHub.Application.Features.Homes.JoinHome;
using HomeHub.Application.Features.Homes.ManageHome;
using HomeHub.Domain.Homes;
using HomeHub.Infrastructure.Identity;
using HomeHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Infrastructure.Homes;

public sealed class HomeService(ApplicationDbContext database, UserManager<ApplicationUser> users, TimeProvider clock)
    : IHomeRegistration, IHomeAccess
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
        finally
        {
            // Identity also saves through this context. Do not leave rolled-back users tracked.
            database.ChangeTracker.Clear();
        }
    }

    public async Task<HomeDetails?> GetAsync(string userId, CancellationToken cancellationToken = default) =>
        await (from membership in database.HomeMemberships
               join home in database.Homes on membership.HomeId equals home.Id
               where membership.UserId == userId
               select new HomeDetails(home.Name, home.OwnerUserId == userId))
            .SingleOrDefaultAsync(cancellationToken);

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
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<InvitationResult> InviteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var home = await database.Homes.SingleOrDefaultAsync(home => home.OwnerUserId == userId, cancellationToken);
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
            database.HomeMemberships.Add(new HomeMembership(home.Id, userId));
            return null;
        }

        var hash = Hash(choice.InvitationCode!.Trim().ToUpperInvariant());
        var invitation = await database.HomeInvitations.SingleOrDefaultAsync(invite => invite.CodeHash == hash, cancellationToken);
        if (invitation is null || !invitation.IsAvailable(clock.GetUtcNow()))
            return "That invitation code is invalid, expired or already used. Ask the home owner for a new code.";
        database.HomeMemberships.Add(invitation.Redeem(userId, clock.GetUtcNow()));
        return null;
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
