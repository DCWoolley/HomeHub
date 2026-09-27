using HomeHub.Application.Features.Homes.JoinHome;

namespace HomeHub.Application.Features.Homes.ManageHome;

public sealed record HomeDetails(string Name, bool IsOwner, IReadOnlyList<FloorDetails> Floors, string TimeZoneId = "UTC");
public sealed record FloorDetails(Guid Id, string Name, IReadOnlyList<RoomDetails> Rooms);
public sealed record RoomDetails(Guid Id, string Name);
public sealed record InvitationResult(string? Code, DateTimeOffset? ExpiresAt, string? Error);
public sealed record InvitationDetails(Guid Id, DateTimeOffset ExpiresAt, bool Used, bool Revoked);

// Callers supply the authenticated user's ID from the server principal, never from form input.
public interface IHomeAccess
{
    Task<HomeDetails?> GetAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> JoinAsync(string userId, HomeChoice choice, CancellationToken cancellationToken = default);
    Task<InvitationResult> InviteAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> RenameAsync(string userId, string name, CancellationToken cancellationToken = default);
    Task<string?> AddFloorAsync(string userId, string name, CancellationToken cancellationToken = default);
    Task<string?> AddRoomAsync(string userId, Guid floorId, string name, CancellationToken cancellationToken = default);
    Task<string?> ChangeTimeZoneAsync(string userId, string timeZoneId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvitationDetails>> GetInvitationsAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> RevokeInvitationAsync(string userId, Guid invitationId, CancellationToken cancellationToken = default);
}
