namespace HomeHub.Web.Modules.MealPlanner;

public sealed class Meal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
}

public enum MealTime { Breakfast, Lunch, Dinner }

public sealed class PlannedMeal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid MealId { get; set; }
    public Meal Meal { get; set; } = null!;
    public DateOnly Date { get; set; }
    public MealTime Time { get; set; }
}
