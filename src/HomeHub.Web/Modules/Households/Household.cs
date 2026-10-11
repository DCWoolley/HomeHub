namespace HomeHub.Web.Modules.Households;

public sealed class Household
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int Version { get; set; }
}

public sealed class HouseholdMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = "";
    public bool IsRetired { get; set; }
}

public sealed class HouseholdMembership
{
    public string UserId { get; set; } = "";
    public Guid HouseholdId { get; set; }
    public Guid MemberId { get; set; }
    public bool IsOwner { get; set; }
}

public sealed class HouseholdInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
