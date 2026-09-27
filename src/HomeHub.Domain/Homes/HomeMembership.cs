namespace HomeHub.Domain.Homes;

public sealed class HomeMembership
{
    private HomeMembership() { }

    public HomeMembership(Guid homeId, string userId)
    {
        if (homeId == Guid.Empty) throw new ArgumentException("A home is required.", nameof(homeId));
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        HomeId = homeId;
        UserId = userId;
        MemberId = Guid.NewGuid();
    }

    public Guid HomeId { get; private set; }
    public Guid MemberId { get; private set; }
    public string UserId { get; private set; } = "";
}
