namespace HomeHub.Application.Features.Homes.JoinHome;

public sealed record HomeChoice(string? HomeName, string? InvitationCode)
{
    public string? Validate()
    {
        var create = !string.IsNullOrWhiteSpace(HomeName);
        var join = !string.IsNullOrWhiteSpace(InvitationCode);
        if (create == join) return "Enter either a home name or an invitation code, not both.";
        if (create && HomeName!.Trim().Length > 100) return "Home names must be 100 characters or fewer.";
        if (join && InvitationCode!.Trim().Length != 32) return "Enter a valid 32-character invitation code.";
        return null;
    }
}
