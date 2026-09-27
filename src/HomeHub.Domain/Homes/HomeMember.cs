namespace HomeHub.Domain.Homes;

public sealed class HomeMember
{
    private HomeMember() { }

    internal HomeMember(Guid homeId, string displayName, string? colour)
    {
        Id = Guid.NewGuid();
        HomeId = homeId;
        SetProfile(displayName, colour);
    }

    public HomeMember(HomeMembership membership, string displayName)
        : this(membership.HomeId, displayName, null)
    {
        Id = membership.MemberId;
        IsAccountLinked = true;
    }

    public Guid Id { get; private set; }
    public Guid HomeId { get; private set; }
    public string DisplayName { get; private set; } = "";
    public string? Colour { get; private set; }
    public bool IsAccountLinked { get; private set; }
    public bool IsRetired { get; private set; }

    internal void Edit(string displayName, string? colour)
    {
        if (IsAccountLinked || IsRetired)
            throw new InvalidOperationException("Only active non-user profiles can be edited here.");
        SetProfile(displayName, colour);
    }

    internal void Retire() => IsRetired = true;

    private void SetProfile(string displayName, string? colour)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 100)
            throw new ArgumentException("Enter a display name of up to 100 characters.", nameof(displayName));
        if (!string.IsNullOrEmpty(colour) &&
            (colour.Length != 7 || colour[0] != '#' || colour.Skip(1).Any(character => !Uri.IsHexDigit(character))))
            throw new ArgumentException("Choose a colour in #RRGGBB format.", nameof(colour));
        DisplayName = displayName.Trim();
        Colour = string.IsNullOrEmpty(colour) ? null : colour.ToUpperInvariant();
    }
}
