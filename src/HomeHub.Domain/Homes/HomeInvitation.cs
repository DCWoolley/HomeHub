namespace HomeHub.Domain.Homes;

public sealed class HomeInvitation
{
    private HomeInvitation() { }

    internal HomeInvitation(Guid homeId, string codeHash, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        Id = Guid.NewGuid();
        HomeId = homeId;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid HomeId { get; private set; }
    public string CodeHash { get; private set; } = "";
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool Used { get; private set; }

    public bool IsAvailable(DateTimeOffset now) => !Used && now < ExpiresAt;

    public HomeMembership Redeem(string userId, DateTimeOffset now)
    {
        if (!IsAvailable(now)) throw new InvalidOperationException("This invitation is no longer available.");
        var membership = new HomeMembership(HomeId, userId);
        Used = true;
        return membership;
    }
}
