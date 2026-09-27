using HomeHub.Application.Features.Homes.ManageMembers;
using HomeHub.Application.Features.Meals.ManageMeals;
using HomeHub.Application.Features.Meals.PlanMeals;
using HomeHub.Domain.Meals;
using HomeHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Infrastructure.Meals;

public sealed class MealService(ApplicationDbContext database, IHomeMembers homeMembers) : IMeals, IMealPlanner
{
    private IQueryable<Meal> AccessibleMeals(string userId) => database.Meals.Where(meal =>
        database.HomeMemberships.Any(link => link.UserId == userId && link.HomeId == meal.HomeId));

    private IQueryable<PlannedMeal> AccessiblePlan(string userId) => database.PlannedMeals.Where(entry =>
        database.Meals.Any(meal => meal.Id == entry.MealId &&
            database.HomeMemberships.Any(link => link.UserId == userId && link.HomeId == meal.HomeId)));

    public async Task<IReadOnlyList<MealDetails>?> GetMealsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!await database.HomeMemberships.AnyAsync(link => link.UserId == userId, cancellationToken)) return null;
        var meals = await AccessibleMeals(userId).AsNoTracking().OrderBy(meal => meal.Name).ToListAsync(cancellationToken);
        var plan = await AccessiblePlan(userId).AsNoTracking().ToListAsync(cancellationToken);
        return meals.Select(meal => new MealDetails(meal.Id, meal.Name,
            plan.Where(entry => entry.MealId == meal.Id).Select(entry => entry.Day).Distinct().Order().ToArray())).ToArray();
    }

    public async Task<string?> SaveMealAsync(string userId, Guid? mealId, string name, CancellationToken cancellationToken = default)
    {
        var homeId = await database.HomeMemberships.Where(link => link.UserId == userId)
            .Select(link => (Guid?)link.HomeId).SingleOrDefaultAsync(cancellationToken);
        if (homeId is null) return "Create or join a home first.";
        try
        {
            if (mealId is null) database.Meals.Add(new Meal(homeId.Value, name));
            else
            {
                var meal = await AccessibleMeals(userId).SingleOrDefaultAsync(meal => meal.Id == mealId, cancellationToken);
                if (meal is null) return "This meal is no longer available in your home.";
                meal.Rename(name);
            }
        }
        catch (ArgumentException exception) { return exception.Message; }
        return await SaveAsync(cancellationToken);
    }

    public async Task<string?> DeleteMealAsync(string userId, Guid mealId, CancellationToken cancellationToken = default)
    {
        var meal = await AccessibleMeals(userId).SingleOrDefaultAsync(meal => meal.Id == mealId, cancellationToken);
        if (meal is null) return "This meal is no longer available in your home.";
        database.Meals.Remove(meal);
        return await SaveAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlannedMealDetails>?> GetPlanAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!await database.HomeMemberships.AnyAsync(link => link.UserId == userId, cancellationToken)) return null;
        return await (from entry in AccessiblePlan(userId).AsNoTracking()
                      join meal in database.Meals on entry.MealId equals meal.Id
                      orderby entry.Day, meal.Name, entry.Id
                      select new PlannedMealDetails(entry.Id, meal.Id, meal.Name, entry.Day,
                          entry.Members.Select(member => member.MemberId).ToList())).ToListAsync(cancellationToken);
    }

    public async Task<string?> AddMealAsync(string userId, Guid mealId, DayOfWeek day, CancellationToken cancellationToken = default)
    {
        var meal = await AccessibleMeals(userId).SingleOrDefaultAsync(meal => meal.Id == mealId, cancellationToken);
        if (meal is null) return "Choose a meal from your household meal list.";
        try { database.PlannedMeals.Add(new PlannedMeal(meal, day)); }
        catch (ArgumentException exception) { return exception.Message; }
        return await SaveAsync(cancellationToken);
    }

    public async Task<string?> SetMembersAsync(string userId, Guid entryId, IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken = default)
    {
        var entry = await AccessiblePlan(userId).Include(entry => entry.Members)
            .SingleOrDefaultAsync(entry => entry.Id == entryId, cancellationToken);
        if (entry is null) return "This planned meal is no longer available in your home.";
        var active = await homeMembers.GetMembersAsync(userId, cancellationToken);
        if (active is null || memberIds.Any(id => active.Members.All(member => member.Id != id)))
            return "Choose only active members of your household.";
        entry.AssignMembers(memberIds);
        return await SaveAsync(cancellationToken);
    }

    public async Task<string?> RemoveMealAsync(string userId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await AccessiblePlan(userId).SingleOrDefaultAsync(entry => entry.Id == entryId, cancellationToken);
        if (entry is null) return "This planned meal is no longer available in your home.";
        database.PlannedMeals.Remove(entry);
        return await SaveAsync(cancellationToken);
    }

    private async Task<string?> SaveAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            return "This meal plan has changed. Reload the page and try again.";
        }
        return null;
    }
}
