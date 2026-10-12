using HomeHub.Web.Data;
using HomeHub.Web.Modules.Account.Register;
using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.Households.ManageAccountMembers;
using HomeHub.Web.Modules.Households.ManageInvitations;
using HomeHub.Web.Modules.Households.TransferOwnership;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class SqlServerTests
{
    [LocalDbFact]
    public async Task SingleScheduleMigrationResetsTestPlansButKeepsMealLists()
    {
        var name = "HomeHub_RestartTest_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;MultipleActiveResultSets=True";
        try
        {
            using var app = new RestartFactory(connection);
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.GetService<IMigrator>().MigrateAsync("20261011191806_AddMeals");
            var register = app.Services.GetRequiredService<RegisterAccount>();
            var owner = await register.RunAsync(RegistrationTests.Request());
            var foreign = await register.RunAsync(RegistrationTests.Request("foreign@example.test"));
            var actor = MembershipTests.Actor(owner.UserId!);
            var stranger = MembershipTests.Actor(foreign.UserId!);
            var meals = app.Services.GetRequiredService<HomeHub.Web.Modules.MealPlanner.ManageMeals.ManageMeals>();
            var first = await meals.SaveAsync(actor, new() { Name = "Eggs" });
            var second = await meals.SaveAsync(actor, new() { Name = "Toast" });
            var other = await meals.SaveAsync(stranger, new() { Name = "Eggs" });
            var household = (await db.HouseholdMemberships.SingleAsync(x => x.UserId == owner.UserId)).HouseholdId;
            var foreignHousehold = (await db.HouseholdMemberships.SingleAsync(x => x.UserId == foreign.UserId)).HouseholdId;
            var newest = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [PlannedMeals] ([Id], [HouseholdId], [MealId], [Date], [Time]) VALUES
                ({Guid.NewGuid()}, {household}, {first}, '20261012', 0),
                ({newest}, {household}, {first}, '20261019', 0),
                ({Guid.NewGuid()}, {household}, {second}, '20261012', 0),
                ({Guid.NewGuid()}, {household}, {first}, '20261018', 2),
                ({Guid.NewGuid()}, {foreignHousehold}, {other}, '20261012', 0);
                """);
            await db.Database.MigrateAsync();
            var planner = app.Services.GetRequiredService<HomeHub.Web.Modules.MealPlanner.PlanWeek.PlanWeek>();
            var schedule = await planner.GetAsync(actor);
            Assert.Empty(schedule);
            Assert.Empty(await planner.GetAsync(stranger));
            Assert.Equal(2, (await meals.ListAsync(actor)).Count);
            Assert.Single(await meals.ListAsync(stranger));
            await planner.SetAsync(actor, HomeHub.Web.Modules.MealPlanner.MealDay.Sunday, HomeHub.Web.Modules.MealPlanner.MealTime.Dinner, first);
            Assert.Single(await planner.GetAsync(actor));
        }
        finally
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
            if (db.Database.GetDbConnection().Database != name || !name.StartsWith("HomeHub_RestartTest_"))
                throw new InvalidOperationException("Unsafe test database cleanup.");
            await db.Database.EnsureDeletedAsync();
        }
    }

    [LocalDbFact]
    public async Task ConcurrentMealCreationPreservesUniqueNames()
    {
        var name = "HomeHub_RestartTest_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;MultipleActiveResultSets=True";
        try
        {
            using var app = new RestartFactory(connection);
            await using (var scope = app.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
            var owner = await app.Services.GetRequiredService<RegisterAccount>().RunAsync(RegistrationTests.Request());
            var actor = MembershipTests.Actor(owner.UserId!);
            var meals = app.Services.GetRequiredService<HomeHub.Web.Modules.MealPlanner.ManageMeals.ManageMeals>();
            async Task<bool> Create(string mealName)
            {
                try { await meals.SaveAsync(actor, new() { Name = mealName }); return true; }
                catch (HouseholdRuleException) { return false; }
            }
            var results = await Task.WhenAll(Create("Pasta"), Create(" pasta "));
            Assert.Single(results, x => x);
            Assert.Single(await meals.ListAsync(actor));
            var create = app.Services.GetRequiredService<HomeHub.Web.Modules.MealPlanner.CreateMealFromPlanner.CreateMealFromPlanner>();
            var day = HomeHub.Web.Modules.MealPlanner.MealDay.Monday;
            var inlineResults = await Task.WhenAll(
                create.RunAsync(actor, new() { Name = "Soup", Day = day, Time = HomeHub.Web.Modules.MealPlanner.MealTime.Dinner }),
                create.RunAsync(actor, new() { Name = " soup ", Day = day, Time = HomeHub.Web.Modules.MealPlanner.MealTime.Dinner }));
            Assert.Single(inlineResults, x => !x.Reused);
            Assert.Single(inlineResults, x => x.AlreadyPlanned);
            Assert.Equal(2, (await meals.ListAsync(actor)).Count);
            Assert.Single(await app.Services.GetRequiredService<HomeHub.Web.Modules.MealPlanner.PlanWeek.PlanWeek>().GetAsync(actor));
        }
        finally
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
            if (db.Database.GetDbConnection().Database != name || !name.StartsWith("HomeHub_RestartTest_"))
                throw new InvalidOperationException("Unsafe test database cleanup.");
            await db.Database.EnsureDeletedAsync();
        }
    }

    [LocalDbFact]
    public async Task FreshMigrationsAndConcurrentMembershipChangesPreserveInvariants()
    {
        var name = "HomeHub_RestartTest_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;MultipleActiveResultSets=True";
        try
        {
            using var app = new RestartFactory(connection);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.MigrateAsync();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                Assert.False(db.Database.HasPendingModelChanges());
            }
            var register = app.Services.GetRequiredService<RegisterAccount>();
            var owner = await register.RunAsync(RegistrationTests.Request());
            Assert.True(owner.Succeeded);
            var actor = MembershipTests.Actor(owner.UserId!);
            var invite = await app.Services.GetRequiredService<ManageInvitations>().CreateAsync(actor);
            var first = RegistrationTests.Request("first@example.test"); first.Choice = HouseholdChoice.Join; first.InvitationCode = invite;
            var second = RegistrationTests.Request("second@example.test"); second.Choice = HouseholdChoice.Join; second.InvitationCode = invite;
            var outcomes = await Task.WhenAll(register.RunAsync(first), register.RunAsync(second));
            Assert.Equal(1, outcomes.Count(x => x.Succeeded));
            var joinedId = outcomes.Single(x => x.Succeeded).UserId!;
            var transfer = app.Services.GetRequiredService<TransferOwnership>();
            var accounts = app.Services.GetRequiredService<ManageAccountMembers>();
            async Task Attempt(Func<Task> operation)
            {
                try { await operation(); } catch (HouseholdRuleException) { }
            }
            await Task.WhenAll(Attempt(() => transfer.RunAsync(actor, joinedId)), Attempt(() => accounts.RemoveAsync(actor, joinedId)));
            await using var check = app.Services.CreateAsyncScope();
            var dbCheck = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await dbCheck.HouseholdMemberships.CountAsync(x => x.IsOwner));
            Assert.Equal(2, await dbCheck.Users.CountAsync());
            Assert.NotNull(await dbCheck.HouseholdMemberships.SingleOrDefaultAsync(x => x.IsOwner));
        }
        finally
        {
            // Only the isolated database named above is ever deleted by this test.
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
            await using var db = new ApplicationDbContext(options);
            if (db.Database.GetDbConnection().Database != name || !name.StartsWith("HomeHub_RestartTest_"))
                throw new InvalidOperationException("Unsafe test database cleanup.");
            await db.Database.EnsureDeletedAsync();
        }
    }
}
