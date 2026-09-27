namespace HomeHub.Domain.Homes;

public sealed class HomeFloor
{
    private HomeFloor() { }

    internal HomeFloor(Guid homeId, string name)
    {
        Id = Guid.NewGuid();
        HomeId = homeId;
        Name = name;
    }

    public Guid Id { get; private set; }
    public Guid HomeId { get; private set; }
    public string Name { get; private set; } = "";
}
