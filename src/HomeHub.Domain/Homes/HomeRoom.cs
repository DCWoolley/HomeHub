namespace HomeHub.Domain.Homes;

public sealed class HomeRoom
{
    private HomeRoom() { }

    internal HomeRoom(Guid floorId, string name)
    {
        Id = Guid.NewGuid();
        FloorId = floorId;
        Name = name;
    }

    public Guid Id { get; private set; }
    public Guid FloorId { get; private set; }
    public string Name { get; private set; } = "";
}
