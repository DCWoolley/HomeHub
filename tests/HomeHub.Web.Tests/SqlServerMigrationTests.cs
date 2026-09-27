using HomeHub.Infrastructure.Persistence;
using HomeHub.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class SqlServerMigrationTests
{
    [LocalDbFact]
    public async Task ExistingMembershipsAreBackfilledWithoutChangingOwnershipOrInvitations()
    {
        var databaseName = "HomeHub_MigrationTest_" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true"
        }).Build());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            await database.GetService<IMigrator>().MigrateAsync("20260926212800_AddHomeFloorsAndRooms");
            await database.Database.ExecuteSqlRawAsync("""
                INSERT INTO [AspNetUsers] ([Id], [UserName], [EmailConfirmed], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount])
                VALUES ('owner', 'Owner', 1, 0, 0, 0, 0), ('member', 'Member', 1, 0, 0, 0, 0);
                INSERT INTO [Homes] ([Id], [Name], [OwnerUserId]) VALUES ('00000000-0000-0000-0000-000000000001', 'Existing home', 'owner');
                INSERT INTO [HomeMemberships] ([UserId], [HomeId]) VALUES
                    ('owner', '00000000-0000-0000-0000-000000000001'), ('member', '00000000-0000-0000-0000-000000000001');
                INSERT INTO [HomeInvitations] ([Id], [HomeId], [CodeHash], [ExpiresAt], [Used])
                VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'existing-hash', '2030-01-01', 0);
                """);
            await database.Database.MigrateAsync();
            var home = await database.Homes.SingleAsync();
            Assert.Equal("Existing home", home.Name);
            Assert.Equal("owner", home.OwnerUserId);
            Assert.Equal("UTC", home.TimeZoneId);
            var links = await database.HomeMemberships.ToListAsync();
            var profiles = await database.HomeMembers.ToListAsync();
            Assert.Equal(2, profiles.Count);
            Assert.Equal(2, links.Select(link => link.MemberId).Distinct().Count());
            foreach (var link in links)
            {
                Assert.NotEqual(Guid.Empty, link.MemberId);
                var profile = Assert.Single(profiles, profile => profile.Id == link.MemberId);
                Assert.Equal(home.Id, profile.HomeId);
                Assert.True(profile.IsAccountLinked);
                Assert.False(profile.IsRetired);
            }
            Assert.Equal("Owner", profiles.Single(profile => profile.Id == links.Single(link => link.UserId == "owner").MemberId).DisplayName);
            var invite = await database.HomeInvitations.SingleAsync();
            Assert.Equal("existing-hash", invite.CodeHash);
            Assert.False(invite.Revoked);
            Assert.False(invite.Used);
            Assert.Empty(await database.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            // This context only ever targets the unique LocalDB database created above.
            await database.Database.EnsureDeletedAsync();
        }
    }

    private sealed class LocalDbFactAttribute : FactAttribute
    {
        public LocalDbFactAttribute()
        {
            if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("HOMEHUB_TEST_LOCALDB") != "1")
                Skip = "Set HOMEHUB_TEST_LOCALDB=1 on Windows with SQL Server LocalDB to verify SQL Server migration backfill.";
        }
    }
}
