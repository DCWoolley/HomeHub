using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.MealPlanner;

internal static class MealSlotAccess
{
    internal static async Task<MealPlannerSettings> ReadAsync(ApplicationDbContext db, Guid householdId, CancellationToken ct) =>
        await db.MealPlannerSettings.AsNoTracking().SingleOrDefaultAsync(x => x.HouseholdId == householdId, ct)
        ?? new MealPlannerSettings { HouseholdId = householdId };

    internal static async Task RequireEnabledAsync(ApplicationDbContext db, Guid householdId, MealTime time, CancellationToken ct)
    {
        if (!(await ReadAsync(db, householdId, ct)).IsEnabled(time))
            throw new HouseholdRuleException($"{time} is disabled in planner settings. Refresh the schedule or enable it in settings.");
    }
}
