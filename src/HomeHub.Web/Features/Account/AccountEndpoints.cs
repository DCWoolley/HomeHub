using HomeHub.Web.Features.Account.ExternalLogin;
using HomeHub.Web.Features.Account.Login;
using HomeHub.Web.Features.Account.Logout;
using HomeHub.Web.Features.Account.Manage.ExternalLogins;
using HomeHub.Web.Features.Account.Manage.Passkeys;
using HomeHub.Web.Features.Account.Manage.PersonalData;

namespace HomeHub.Web.Features.Account;

internal static class AccountEndpoints
{
    public static IEndpointConventionBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var accountGroup = endpoints.MapGroup("/Account");
        accountGroup.MapExternalLogin();
        accountGroup.MapLogout();
        accountGroup.MapCreatePasskeyOptions();
        accountGroup.MapRequestPasskeyOptions();

        var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();
        manageGroup.MapLinkExternalLogin();
        manageGroup.MapDownloadPersonalData();

        return accountGroup;
    }
}
