namespace HomeHub.Web.Modules.MealPlanner;

public sealed class MealPlannerSettings
{
    public Guid HouseholdId { get; set; }
    public bool BreakfastEnabled { get; set; }
    public bool LunchEnabled { get; set; }
    public bool DinnerEnabled { get; set; } = true;

    public bool IsEnabled(MealTime time) => time switch
    {
        MealTime.Breakfast => BreakfastEnabled,
        MealTime.Lunch => LunchEnabled,
        MealTime.Dinner => DinnerEnabled,
        _ => false
    };
}
