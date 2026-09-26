using HomeHub.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HomeHub.Web.Features.Account.Logout;

internal static class LogoutEndpoint
{
    public static RouteHandlerBuilder MapLogout(this RouteGroupBuilder group)
    {
        return group.MapPost("/Logout", async (
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect($"~/{returnUrl}");
        });
    }
}
