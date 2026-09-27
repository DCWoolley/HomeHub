namespace HomeHub.Domain.Homes;

public sealed class Home
{
    private Home() { }

    public Home(string name, string ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Enter a home name of up to 100 characters.", nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        Id = Guid.NewGuid();
        Name = name.Trim();
        OwnerUserId = ownerUserId;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string OwnerUserId { get; private set; } = "";
    public string TimeZoneId { get; private set; } = "UTC";

    public void ChangeTimeZone(string requesterId, string timeZoneId)
    {
        EnsureOwner(requesterId);
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Length > 100 ||
            !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
            throw new ArgumentException("Choose a valid timezone.", nameof(timeZoneId));
        TimeZoneId = timeZoneId;
    }

    public HomeMember CreateProfile(string requesterId, string name, string? colour)
    {
        EnsureOwner(requesterId);
        return new HomeMember(Id, name, colour);
    }

    public void EditProfile(string requesterId, HomeMember member, string name, string? colour)
    {
        EnsureMemberOwner(requesterId, member);
        member.Edit(name, colour);
    }

    public void RemoveMember(string requesterId, HomeMember member, HomeMembership? membership)
    {
        EnsureMemberOwner(requesterId, member);
        if (membership is not null && (membership.HomeId != Id || membership.MemberId != member.Id))
            throw new ArgumentException("The membership does not match this profile.", nameof(membership));
        if (member.IsAccountLinked && membership is null && !member.IsRetired)
            throw new InvalidOperationException("An account-linked member requires its current membership.");
        if (membership?.UserId == OwnerUserId)
            throw new InvalidOperationException("The owner cannot remove themselves.");
        member.Retire();
    }

    private void EnsureMemberOwner(string requesterId, HomeMember member)
    {
        EnsureOwner(requesterId);
        if (member.HomeId != Id) throw new ArgumentException("Choose a member of this home.", nameof(member));
    }

    public void RevokeInvitation(string requesterId, HomeInvitation invitation)
    {
        EnsureOwner(requesterId);
        if (invitation.HomeId != Id) throw new ArgumentException("Choose an invitation for this home.", nameof(invitation));
        invitation.Revoke();
    }

    public void Rename(string requesterId, string name)
    {
        EnsureOwner(requesterId);
        Name = ValidateName(name);
    }

    public HomeFloor AddFloor(string requesterId, string name)
    {
        EnsureOwner(requesterId);
        return new HomeFloor(Id, ValidateName(name));
    }

    public HomeRoom AddRoom(string requesterId, HomeFloor floor, string name)
    {
        EnsureOwner(requesterId);
        if (floor.HomeId != Id)
            throw new ArgumentException("Choose a floor in this home.", nameof(floor));
        return new HomeRoom(floor.Id, ValidateName(name));
    }

    private void EnsureOwner(string requesterId)
    {
        if (requesterId != OwnerUserId)
            throw new UnauthorizedAccessException("Only the home owner can configure this home.");
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Enter a name of up to 100 characters.", nameof(name));
        return name.Trim();
    }

    public HomeInvitation Invite(string requesterId, string codeHash, DateTimeOffset now)
    {
        if (requesterId != OwnerUserId)
            throw new UnauthorizedAccessException("Only the home owner can invite members.");
        return new HomeInvitation(Id, codeHash, now.AddDays(7));
    }
}
