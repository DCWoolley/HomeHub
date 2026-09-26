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

    public HomeInvitation Invite(string requesterId, string codeHash, DateTimeOffset now)
    {
        if (requesterId != OwnerUserId)
            throw new UnauthorizedAccessException("Only the home owner can invite members.");
        return new HomeInvitation(Id, codeHash, now.AddDays(7));
    }
}
