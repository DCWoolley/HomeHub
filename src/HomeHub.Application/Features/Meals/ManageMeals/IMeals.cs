namespace HomeHub.Application.Features.Meals.ManageMeals;

public sealed record MealDetails(Guid Id, string Name, IReadOnlyList<DayOfWeek> PlannedDays);

public interface IMeals
{
    Task<IReadOnlyList<MealDetails>?> GetMealsAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> SaveMealAsync(string userId, Guid? mealId, string name, CancellationToken cancellationToken = default);
    Task<string?> DeleteMealAsync(string userId, Guid mealId, CancellationToken cancellationToken = default);
}
