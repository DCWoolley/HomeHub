using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.MealPlanner.ManagePlannerSettings;

public sealed class ManagePlannerSettings(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed class Request
    {
        public bool BreakfastEnabled { get; set; }
        public bool LunchEnabled { get; set; }
        public bool DinnerEnabled { get; set; } = true;
    }

    public async Task<Request> GetAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        var settings = await MealSlotAccess.ReadAsync(db, current.HouseholdId, ct);
        return new() { BreakfastEnabled = settings.BreakfastEnabled, LunchEnabled = settings.LunchEnabled, DinnerEnabled = settings.DinnerEnabled };
    }

    public async Task SaveAsync(ClaimsPrincipal actor, Request input, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        var settings = await db.MealPlannerSettings.SingleOrDefaultAsync(x => x.HouseholdId == current.HouseholdId, ct);
        if (settings is null)
        {
            settings = new() { HouseholdId = current.HouseholdId };
            db.MealPlannerSettings.Add(settings);
        }
        settings.BreakfastEnabled = input.BreakfastEnabled;
        settings.LunchEnabled = input.LunchEnabled;
        settings.DinnerEnabled = input.DinnerEnabled;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
