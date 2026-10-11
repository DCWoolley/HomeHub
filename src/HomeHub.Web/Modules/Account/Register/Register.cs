using System.ComponentModel.DataAnnotations;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.Account.Register;

public sealed class RegisterAccount(IServiceScopeFactory scopes, TimeProvider time)
{
    public sealed class Request
    {
        [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
        [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";
        [Required, Compare(nameof(Password))] public string ConfirmPassword { get; set; } = "";
        [Required, StringLength(100)] public string Name { get; set; } = "";
        public HouseholdChoice Choice { get; set; }
        [StringLength(100)] public string? HouseholdName { get; set; }
        [StringLength(100)] public string? InvitationCode { get; set; }
    }

    public sealed record Response(bool Succeeded, string? UserId, IReadOnlyList<string> Errors);

    public async Task<Response> RunAsync(Request input, CancellationToken ct = default)
    {
        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), validation, true))
            return new(false, null, validation.Select(x => x.ErrorMessage ?? "Invalid input.").ToArray());

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var user = new ApplicationUser { UserName = input.Email.Trim(), Email = input.Email.Trim() };
            var identity = await users.CreateAsync(user, input.Password);
            if (!identity.Succeeded)
                return new(false, null, identity.Errors.Select(x => x.Description).ToArray());
            await HouseholdEnrollment.ApplyAsync(db, user.Id, input.Name, input.Choice,
                input.HouseholdName, input.InvitationCode, time.GetUtcNow().UtcDateTime, ct);
            await transaction.CommitAsync(ct);
            return new(true, user.Id, []);
        }
        catch (HouseholdRuleException error) { return new(false, null, [error.Message]); }
        catch (DbUpdateException) { return new(false, null, ["Registration could not be completed. Check your details and invitation, then try again."]); }
    }
}
