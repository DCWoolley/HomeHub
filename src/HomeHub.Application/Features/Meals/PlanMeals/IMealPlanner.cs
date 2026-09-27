namespace HomeHub.Application.Features.Meals.PlanMeals;

public sealed record PlannedMealDetails(Guid Id, Guid MealId, string Name, DayOfWeek Day, IReadOnlyList<Guid> MemberIds);

public interface IMealPlanner
{
    Task<IReadOnlyList<PlannedMealDetails>?> GetPlanAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> AddMealAsync(string userId, Guid mealId, DayOfWeek day, CancellationToken cancellationToken = default);
    Task<string?> SetMembersAsync(string userId, Guid entryId, IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken = default);
    Task<string?> RemoveMealAsync(string userId, Guid entryId, CancellationToken cancellationToken = default);
}
