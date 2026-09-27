using HomeHub.Application.Features.Homes.ManageMembers;
using HomeHub.Domain.Meals;
using HomeHub.Infrastructure.Homes;
using HomeHub.Infrastructure.Identity;
using HomeHub.Infrastructure.Meals;
using HomeHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class MealPlannerTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ServiceProvider provider;
    private readonly IServiceScope scope;
    private ApplicationDbContext Database => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    private HomeService Homes => scope.ServiceProvider.GetRequiredService<HomeService>();
    private MealService Meals => scope.ServiceProvider.GetRequiredService<MealService>();

    public MealPlannerTests()
    {
        connection.Open();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<HomeService>();
        services.AddScoped<IHomeMembers, HomeService>();
        services.AddScoped<MealService>();
        provider = services.BuildServiceProvider();
        scope = provider.CreateScope();
        Database.Database.EnsureCreated();
    }

    private async Task<string> Register(string email, string? home = "Home", string? code = null) =>
        (await Homes.RegisterAsync(new(email, "StrongPassword123!", new(home, code)))).UserId!;

    [Fact]
    public async Task SharedPlanLinksMealsAndCascadesDeletionAcrossDays()
    {
        var owner = await Register("owner@example.test");
        var member = await Register("member@example.test", null, (await Homes.InviteAsync(owner)).Code);
        Assert.Null(await Meals.SaveMealAsync(member, null, "  Pasta  "));
        var meal = Assert.Single((await Meals.GetMealsAsync(owner))!);
        Assert.Equal("Pasta", meal.Name);
        Assert.Null(await Meals.AddMealAsync(owner, meal.Id, DayOfWeek.Monday));
        Assert.Null(await Meals.AddMealAsync(member, meal.Id, DayOfWeek.Friday));
        var monday = (await Meals.GetPlanAsync(member))!.First();
        Assert.Empty(monday.MemberIds);
        var profile = (await Homes.GetMembersAsync(owner))!.Members.First();
        Assert.Null(await Meals.SetMembersAsync(member, monday.Id, [profile.Id]));
        Assert.Null(await Meals.SaveMealAsync(owner, meal.Id, "Noodles"));
        Database.ChangeTracker.Clear();
        var plan = (await Meals.GetPlanAsync(member))!;
        Assert.All(plan, entry => Assert.Equal("Noodles", entry.Name));
        Assert.Equal(profile.Id, Assert.Single(plan.First().MemberIds));
        Assert.Empty(plan.Last().MemberIds);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Friday }, Assert.Single((await Meals.GetMealsAsync(owner))!).PlannedDays);
        Assert.Null(await Meals.DeleteMealAsync(member, meal.Id));
        Database.ChangeTracker.Clear();
        Assert.Empty((await Meals.GetPlanAsync(owner))!);
        Assert.Empty(await Database.Set<PlannedMealMember>().ToListAsync());
        Assert.Empty((await Meals.GetMealsAsync(owner))!);
    }

    [Fact]
    public async Task MemberExceptionsArePerEntryAndClearingReturnsToEveryone()
    {
        var owner = await Register("owner@example.test");
        await Homes.CreateProfileAsync(owner, "Child", null);
        var child = (await Homes.GetMembersAsync(owner))!.Members.Single(member => !member.IsAccountLinked);
        await Meals.SaveMealAsync(owner, null, "Soup");
        var meal = Assert.Single((await Meals.GetMealsAsync(owner))!);
        await Meals.AddMealAsync(owner, meal.Id, DayOfWeek.Monday);
        await Meals.AddMealAsync(owner, meal.Id, DayOfWeek.Monday);
        var plan = (await Meals.GetPlanAsync(owner))!;
        Assert.Equal(2, plan.Count);
        Assert.Null(await Meals.SetMembersAsync(owner, plan[0].Id, [child.Id, child.Id]));
        Database.ChangeTracker.Clear();
        Assert.Single((await Meals.GetPlanAsync(owner))!.Single(entry => entry.Id == plan[0].Id).MemberIds);
        Assert.Null(await Meals.SetMembersAsync(owner, plan[0].Id, []));
        Database.ChangeTracker.Clear();
        Assert.All((await Meals.GetPlanAsync(owner))!, entry => Assert.Empty(entry.MemberIds));
        await Meals.SetMembersAsync(owner, plan[0].Id, [child.Id]);
        Assert.Null(await Meals.RemoveMealAsync(owner, plan[0].Id));
        Database.ChangeTracker.Clear();
        Assert.Single((await Meals.GetPlanAsync(owner))!);
        Assert.Single((await Meals.GetMealsAsync(owner))!);
        Assert.Empty(await Database.Set<PlannedMealMember>().ToListAsync());
    }

    [Fact]
    public async Task RejectsForeignIdsRetiredMembersAndRevokedAccess()
    {
        var owner = await Register("owner@example.test");
        var other = await Register("other@example.test");
        var member = await Register("member@example.test", null, (await Homes.InviteAsync(owner)).Code);
        await Meals.SaveMealAsync(owner, null, "Pasta");
        var meal = Assert.Single((await Meals.GetMealsAsync(owner))!);
        await Meals.AddMealAsync(owner, meal.Id, DayOfWeek.Tuesday);
        var entry = Assert.Single((await Meals.GetPlanAsync(owner))!);
        var foreignProfile = (await Homes.GetMembersAsync(other))!.Members.First();
        Assert.NotNull(await Meals.SetMembersAsync(owner, entry.Id, [foreignProfile.Id]));
        await Homes.CreateProfileAsync(owner, "Child", null);
        var child = (await Homes.GetMembersAsync(owner))!.Members.Single(item => !item.IsAccountLinked);
        await Homes.RemoveMemberAsync(owner, child.Id);
        Assert.NotNull(await Meals.SetMembersAsync(owner, entry.Id, [child.Id]));
        Assert.Empty((await Meals.GetMealsAsync(other))!);
        foreach (var actor in new[] { other, "unknown" })
        {
            Assert.NotNull(await Meals.SaveMealAsync(actor, meal.Id, "Forbidden"));
            Assert.NotNull(await Meals.DeleteMealAsync(actor, meal.Id));
            Assert.NotNull(await Meals.AddMealAsync(actor, meal.Id, DayOfWeek.Monday));
            Assert.NotNull(await Meals.RemoveMealAsync(actor, entry.Id));
            Assert.NotNull(await Meals.SetMembersAsync(actor, entry.Id, []));
        }
        var linked = await Database.HomeMemberships.SingleAsync(link => link.UserId == member);
        await Homes.RemoveMemberAsync(owner, linked.MemberId);
        Assert.Null(await Meals.GetMealsAsync(member));
        Assert.Null(await Meals.GetPlanAsync(member));
        Assert.NotNull(await Meals.SaveMealAsync(member, null, "Forbidden"));
        Assert.NotNull(await Meals.DeleteMealAsync(member, meal.Id));
        Assert.NotNull(await Meals.AddMealAsync(member, meal.Id, DayOfWeek.Monday));
        Assert.NotNull(await Meals.SetMembersAsync(member, entry.Id, []));
        Assert.NotNull(await Meals.RemoveMealAsync(member, entry.Id));
    }

    [Fact]
    public async Task ValidationRejectsBlankNamesAndWeekendsWithoutPersisting()
    {
        var owner = await Register("owner@example.test");
        foreach (var name in new[] { "", "  ", new string('a', 101) })
            Assert.NotNull(await Meals.SaveMealAsync(owner, null, name));
        Assert.Empty((await Meals.GetMealsAsync(owner))!);
        await Meals.SaveMealAsync(owner, null, "Dinner");
        var meal = Assert.Single((await Meals.GetMealsAsync(owner))!);
        foreach (var day in new[] { DayOfWeek.Sunday, DayOfWeek.Saturday, (DayOfWeek)10 })
            Assert.NotNull(await Meals.AddMealAsync(owner, meal.Id, day));
        Assert.Empty((await Meals.GetPlanAsync(owner))!);
        Assert.Throws<ArgumentException>(() => new Meal(Guid.Empty, "Dinner"));
        Assert.Throws<ArgumentException>(() => new PlannedMeal(new Meal(Guid.NewGuid(), "Dinner"), DayOfWeek.Sunday));
    }

    public void Dispose()
    {
        scope.Dispose();
        provider.Dispose();
        connection.Dispose();
    }
}
