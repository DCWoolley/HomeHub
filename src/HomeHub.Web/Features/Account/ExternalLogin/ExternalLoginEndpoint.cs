using HomeHub.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using ExternalLoginPage = HomeHub.Web.Features.Account.ExternalLogin.ExternalLogin;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace HomeHub.Web.Features.Account.ExternalLogin;

internal static class ExternalLoginEndpoint
{
    public static RouteHandlerBuilder MapExternalLogin(this RouteGroupBuilder group)
    {
        return group.MapPost("/PerformExternalLogin", (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string provider,
            [FromForm] string returnUrl) =>
        {
            IEnumerable<KeyValuePair<string, StringValues>> query = [
                new("ReturnUrl", returnUrl),
                new("Action", ExternalLoginPage.LoginCallbackAction)];

            var redirectUrl = UriHelper.BuildRelative(
                context.Request.PathBase,
                "/Account/ExternalLogin",
                QueryString.Create(query));

            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return TypedResults.Challenge(properties, [provider]);
        });
    }
}
