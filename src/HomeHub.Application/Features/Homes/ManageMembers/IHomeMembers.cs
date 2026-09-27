namespace HomeHub.Application.Features.Homes.ManageMembers;

public sealed record MemberDetails(Guid Id, string DisplayName, string? Colour, bool IsOwner, bool IsAccountLinked);
public sealed record MembersDetails(bool IsOwner, IReadOnlyList<MemberDetails> Members);

// Always use the authenticated principal's user ID. Returned members are active and may be assigned work.
public interface IHomeMembers
{
    Task<MembersDetails?> GetMembersAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> CreateProfileAsync(string userId, string name, string? colour, CancellationToken cancellationToken = default);
    Task<string?> EditProfileAsync(string userId, Guid memberId, string name, string? colour, CancellationToken cancellationToken = default);
    Task<string?> RemoveMemberAsync(string userId, Guid memberId, CancellationToken cancellationToken = default);
}
