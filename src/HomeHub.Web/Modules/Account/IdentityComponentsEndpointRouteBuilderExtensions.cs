namespace Microsoft.AspNetCore.Routing;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var account = endpoints.MapGroup("/Account");
        HomeHub.Web.Modules.Account.ExternalLogin.ExternalLoginEndpoint.Map(account);
        HomeHub.Web.Modules.Account.Logout.LogoutEndpoint.Map(account);
        HomeHub.Web.Modules.Account.Login.PasskeyRequestEndpoint.Map(account);
        var manage = account.MapGroup("/Manage").RequireAuthorization();
        HomeHub.Web.Modules.Account.Manage.Passkeys.PasskeyCreationEndpoint.Map(manage);
        HomeHub.Web.Modules.Account.Manage.ExternalLogins.LinkExternalLoginEndpoint.Map(manage);
        HomeHub.Web.Modules.Account.Manage.PersonalData.DownloadPersonalDataEndpoint.Map(manage);
        return account;
    }
}
