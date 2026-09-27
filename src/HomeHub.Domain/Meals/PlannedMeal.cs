namespace HomeHub.Domain.Meals;

public sealed class PlannedMeal
{
    private PlannedMeal() { }
    public PlannedMeal(Meal meal, DayOfWeek day)
    {
        if (day < DayOfWeek.Monday || day > DayOfWeek.Friday)
            throw new ArgumentException("Choose a weekday from Monday to Friday.");
        MealId = meal.Id;
        Day = day;
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid MealId { get; private set; }
    public DayOfWeek Day { get; private set; }
    public List<PlannedMealMember> Members { get; private set; } = [];
    public void AssignMembers(IEnumerable<Guid> memberIds)
    {
        var ids = memberIds.Distinct().ToArray();
        if (ids.Contains(Guid.Empty)) throw new ArgumentException("Choose valid household members.");
        Members.RemoveAll(member => !ids.Contains(member.MemberId));
        foreach (var id in ids.Where(id => Members.All(member => member.MemberId != id)))
            Members.Add(new PlannedMealMember(Id, id));
    }
}

public sealed class PlannedMealMember
{
    private PlannedMealMember() { }
    internal PlannedMealMember(Guid plannedMealId, Guid memberId)
    {
        PlannedMealId = plannedMealId;
        MemberId = memberId;
    }
    public Guid PlannedMealId { get; private set; }
    public Guid MemberId { get; private set; }
}
