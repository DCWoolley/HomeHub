namespace HomeHub.Web.Modules.HouseholdItems;

public sealed class HouseholdItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal? ExpectedWeeklyUsage { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
