using HomeHub.Application.Features.Homes.JoinHome;

namespace HomeHub.Application.Features.Homes.ManageHome;

public sealed record HomeDetails(string Name, bool IsOwner);
public sealed record InvitationResult(string? Code, DateTimeOffset? ExpiresAt, string? Error);

// Callers supply the authenticated user's ID from the server principal, never from form input.
public interface IHomeAccess
{
    Task<HomeDetails?> GetAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> JoinAsync(string userId, HomeChoice choice, CancellationToken cancellationToken = default);
    Task<InvitationResult> InviteAsync(string userId, CancellationToken cancellationToken = default);
}
