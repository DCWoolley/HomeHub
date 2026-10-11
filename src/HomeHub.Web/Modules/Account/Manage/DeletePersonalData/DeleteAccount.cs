using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Account.Manage.DeletePersonalData;

public sealed class DeleteAccount(IServiceScopeFactory scopes)
{
    public async Task<string?> RunAsync(ClaimsPrincipal actor, string password, CancellationToken ct = default)
    {
        var id = HouseholdAccess.UserId(actor);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await db.HouseholdMemberships.AnyAsync(x => x.UserId == id, ct))
            return "Leave your household before deleting your account. Owners must transfer ownership first.";
        var user = await users.FindByIdAsync(id);
        if (user is null) return "Your account is no longer available.";
        if (!await users.CheckPasswordAsync(user, password)) return "Incorrect password.";
        try
        {
            var result = await users.DeleteAsync(user);
            return result.Succeeded ? null : "Account deletion could not be completed.";
        }
        catch (DbUpdateException) { return "Leave your household before deleting your account."; }
    }
}
