using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HomeHub.Web.Data;
using HomeHub.Web.Modules.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Modules.MealPlanner.CreateMealFromPlanner;

public sealed class CreateMealFromPlanner(IDbContextFactory<ApplicationDbContext> factory)
{
    public sealed class Request
    {
        [Required, StringLength(100)] public string Name { get; set; } = "";
        public MealDay Day { get; set; }
        public MealTime Time { get; set; }
    }
    public sealed record Response(Guid MealId, string Name, bool Reused, bool AlreadyPlanned);

    public async Task<Response> RunAsync(ClaimsPrincipal actor, Request input, CancellationToken ct = default)
    {
        var name = ManageMeals.ManageMeals.ValidateName(input.Name);
        PlanWeek.PlanWeek.ValidateSlot(input.Day, input.Time);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await HouseholdAccess.LockAsync(db, actor, ct: ct);
        var normalized = name.ToUpperInvariant();
        var meal = await db.Meals.SingleOrDefaultAsync(x => x.HouseholdId == current.HouseholdId && x.NormalizedName == normalized, ct);
        var reused = meal is not null;
        if (meal is null)
        {
            meal = new Meal { HouseholdId = current.HouseholdId, Name = name, NormalizedName = normalized };
            db.Meals.Add(meal);
        }
        var alreadyPlanned = await db.PlannedMeals.AnyAsync(x => x.HouseholdId == current.HouseholdId && x.Day == input.Day && x.Time == input.Time && x.MealId == meal.Id, ct);
        if (!alreadyPlanned)
            db.PlannedMeals.Add(new PlannedMeal { HouseholdId = current.HouseholdId, Day = input.Day, Time = input.Time, Meal = meal, MealId = meal.Id });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(meal.Id, meal.Name, reused, alreadyPlanned);
    }
}
