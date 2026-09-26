using HomeHub.Application.Features.Homes.JoinHome;

namespace HomeHub.Application.Features.Account.Register;

public sealed record ExternalAccount(string Provider, string Key, string? DisplayName);
public sealed record RegistrationRequest(string Email, string? Password, HomeChoice Home, ExternalAccount? External = null);
public sealed record RegistrationResult(string? UserId, IReadOnlyList<string> Errors);

// Implementations commit the account, membership and invitation redemption atomically.
public interface IHomeRegistration
{
    Task<RegistrationResult> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken = default);
}
