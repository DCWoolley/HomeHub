using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.MealPlanner.PlanWeek;

public sealed class PlanWeek(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Entry(Guid Id, Guid MealId, string Name, MealDay Day, MealTime Time);

    public async Task<IReadOnlyList<Entry>> GetAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        var settings = await MealSlotAccess.ReadAsync(db, current.HouseholdId, ct);
        return await db.PlannedMeals.Where(x => x.HouseholdId == current.HouseholdId &&
                ((x.Time == MealTime.Breakfast && settings.BreakfastEnabled) ||
                 (x.Time == MealTime.Lunch && settings.LunchEnabled) ||
                 (x.Time == MealTime.Dinner && settings.DinnerEnabled)))
            .OrderBy(x => x.Meal.Name).Select(x => new Entry(x.Id, x.MealId, x.Meal.Name, x.Day, x.Time)).ToListAsync(ct);
    }

    public async Task SetAsync(ClaimsPrincipal actor, MealDay day, MealTime time, Guid mealId, Guid? entryId = null, CancellationToken ct = default)
    {
        ValidateSlot(day, time);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        await MealSlotAccess.RequireEnabledAsync(db, current.HouseholdId, time, ct);
        await ManageMeals.ManageMeals.FindAsync(db, current.HouseholdId, mealId, ct);
        if (await db.PlannedMeals.AnyAsync(x => x.HouseholdId == current.HouseholdId && x.Day == day && x.Time == time && x.MealId == mealId && x.Id != entryId, ct))
            throw new HouseholdRuleException("This meal is already in this slot.");
        PlannedMeal entry;
        if (entryId is { } id)
        {
            entry = await db.PlannedMeals.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == current.HouseholdId && x.Day == day && x.Time == time, ct)
                ?? throw new HouseholdRuleException("Planned meal not found. Refresh the plan.");
        }
        else { entry = new() { HouseholdId = current.HouseholdId, Day = day, Time = time }; db.PlannedMeals.Add(entry); }
        entry.MealId = mealId;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RemoveAsync(ClaimsPrincipal actor, Guid entryId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        var entry = await db.PlannedMeals.SingleOrDefaultAsync(x => x.Id == entryId && x.HouseholdId == current.HouseholdId, ct)
            ?? throw new HouseholdRuleException("Planned meal not found. Refresh the plan.");
        await MealSlotAccess.RequireEnabledAsync(db, current.HouseholdId, entry.Time, ct);
        db.PlannedMeals.Remove(entry);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    internal static void ValidateSlot(MealDay day, MealTime time)
    {
        if (!Enum.IsDefined(time) || !Enum.IsDefined(day))
            throw new HouseholdRuleException("Choose a valid weekday and meal time.");
    }
}
