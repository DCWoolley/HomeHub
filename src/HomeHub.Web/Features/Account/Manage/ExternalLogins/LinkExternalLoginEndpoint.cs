using HomeHub.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using ExternalLoginsPage = HomeHub.Web.Features.Account.Manage.ExternalLogins.ExternalLogins;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace HomeHub.Web.Features.Account.Manage.ExternalLogins;

internal static class LinkExternalLoginEndpoint
{
    public static RouteHandlerBuilder MapLinkExternalLogin(this RouteGroupBuilder group)
    {
        return group.MapPost("/LinkExternalLogin", async (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string provider) =>
        {
            // Clear the existing external cookie to ensure a clean login process
            await context.SignOutAsync(IdentityConstants.ExternalScheme);

            var redirectUrl = UriHelper.BuildRelative(
                context.Request.PathBase,
                "/Account/Manage/ExternalLogins",
                QueryString.Create("Action", ExternalLoginsPage.LinkLoginCallbackAction));

            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
            return TypedResults.Challenge(properties, [provider]);
        });
    }
}
