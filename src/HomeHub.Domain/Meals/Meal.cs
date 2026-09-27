namespace HomeHub.Domain.Meals;

public sealed class Meal
{
    private Meal() { }
    public Meal(Guid homeId, string name)
    {
        if (homeId == Guid.Empty) throw new ArgumentException("A home is required.");
        HomeId = homeId;
        Rename(name);
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid HomeId { get; private set; }
    public string Name { get; private set; } = "";
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Enter a meal name of up to 100 characters.");
        Name = name.Trim();
    }
}
