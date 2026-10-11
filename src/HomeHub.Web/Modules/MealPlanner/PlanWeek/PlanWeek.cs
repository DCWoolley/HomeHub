using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.MealPlanner.PlanWeek;

public sealed class PlanWeek(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed record Entry(Guid Id, Guid MealId, string Name, DateOnly Date, MealTime Time);
    public static DateOnly Monday(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    public async Task<IReadOnlyList<Entry>> GetAsync(ClaimsPrincipal actor, DateOnly week, CancellationToken ct = default)
    {
        var start = Monday(week); var end = start.AddDays(6);
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await HouseholdAccess.RequireAsync(db, actor, ct: ct);
        return await db.PlannedMeals.Where(x => x.HouseholdId == current.HouseholdId && x.Date >= start && x.Date <= end)
            .OrderBy(x => x.Meal.Name).Select(x => new Entry(x.Id, x.MealId, x.Meal.Name, x.Date, x.Time)).ToListAsync(ct);
    }

    public async Task SetAsync(ClaimsPrincipal actor, DateOnly date, MealTime time, Guid mealId, Guid? entryId = null, CancellationToken ct = default)
    {
        ValidateSlot(date, time);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        await ManageMeals.ManageMeals.FindAsync(db, current.HouseholdId, mealId, ct);
        if (await db.PlannedMeals.AnyAsync(x => x.HouseholdId == current.HouseholdId && x.Date == date && x.Time == time && x.MealId == mealId && x.Id != entryId, ct))
            throw new HouseholdRuleException("This meal is already in this slot.");
        PlannedMeal entry;
        if (entryId is { } id)
        {
            entry = await db.PlannedMeals.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == current.HouseholdId && x.Date == date && x.Time == time, ct)
                ?? throw new HouseholdRuleException("Planned meal not found. Refresh the plan.");
        }
        else { entry = new() { HouseholdId = current.HouseholdId, Date = date, Time = time }; db.PlannedMeals.Add(entry); }
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
        db.PlannedMeals.Remove(entry);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    internal static void ValidateSlot(DateOnly date, MealTime time)
    {
        if (!Enum.IsDefined(time) || date.Year < 1900 || date.Year > 9998)
            throw new HouseholdRuleException("Choose a valid date between 1900 and 9998 and a meal time.");
    }
}
