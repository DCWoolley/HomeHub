namespace HomeHub.Web.Modules.MealPlanner;

public sealed class Ingredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public Guid UnitId { get; set; }
    public IngredientUnit Unit { get; set; } = null!;
    public decimal Amount { get; set; } = 1m;
    public decimal? PackagePrice { get; set; }
}

public sealed class IngredientUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
}

public sealed class RecipeItem
{
    public Guid HouseholdId { get; set; }
    public Guid MealId { get; set; }
    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public decimal Quantity { get; set; }
}
