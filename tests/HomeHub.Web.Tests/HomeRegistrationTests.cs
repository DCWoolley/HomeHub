using HomeHub.Application.Features.Account.Register;
using HomeHub.Application.Features.Homes.JoinHome;
using HomeHub.Domain.Homes;
using HomeHub.Infrastructure.Homes;
using HomeHub.Infrastructure.Identity;
using HomeHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class HomeRegistrationTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ServiceProvider provider;
    private readonly IServiceScope scope;
    private readonly TestClock clock = new();
    private ApplicationDbContext Database => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    private HomeService Service => scope.ServiceProvider.GetRequiredService<HomeService>();

    public HomeRegistrationTests()
    {
        connection.Open();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<HomeService>();
        provider = services.BuildServiceProvider();
        scope = provider.CreateScope();
        Database.Database.EnsureCreated();
    }

    [Fact]
    public async Task CreatingHomeRegistersOwnerAndMembership()
    {
        var result = await Register("owner@example.test", new("  Our home  ", null));
        Assert.Empty(result.Errors);
        var home = await Service.GetAsync(result.UserId!);
        Assert.Equal("Our home", home!.Name);
        Assert.True(home.IsOwner);
        Assert.Equal(result.UserId, (await Database.Homes.SingleAsync()).OwnerUserId);
        Assert.Single(await Database.HomeMemberships.ToListAsync());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Home", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(null, "short")]
    public async Task InvalidChoiceCreatesNothing(string? name, string? code)
    {
        var result = await Register("new@example.test", new(name, code));
        Assert.Null(result.UserId);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(await Database.Users.ToListAsync());
        Assert.Empty(await Database.Homes.ToListAsync());
    }

    [Fact]
    public async Task InvalidInvitationRollsBackIdentityUser()
    {
        var result = await Register("new@example.test", new(null, new string('A', 32)));
        Assert.Null(result.UserId);
        Assert.Empty(await Database.Users.ToListAsync());
        Assert.Empty(await Database.HomeMemberships.ToListAsync());
    }

    [Fact]
    public async Task InvitationAddsMemberAndCannotBeReused()
    {
        var owner = await Register("owner@example.test", new("Home", null));
        var invitation = await Service.InviteAsync(owner.UserId!);
        Assert.Null(invitation.Error);
        Assert.Equal(clock.GetUtcNow().AddDays(7), invitation.ExpiresAt);
        Assert.NotEqual(invitation.Code, (await Database.HomeInvitations.SingleAsync()).CodeHash);
        var member = await Register("member@example.test", new(null, " " + invitation.Code!.ToLowerInvariant() + " "));
        Assert.Empty(member.Errors);
        Assert.False((await Service.GetAsync(member.UserId!))!.IsOwner);
        Assert.NotNull((await Service.InviteAsync(member.UserId!)).Error);
        var second = await Register("second@example.test", new(null, invitation.Code));
        Assert.Null(second.UserId);
        Assert.Equal(2, await Database.Users.CountAsync());
    }

    [Fact]
    public async Task ExpiredInvitationDoesNotCreateAccount()
    {
        var owner = await Register("owner@example.test", new("Home", null));
        var invitation = await Service.InviteAsync(owner.UserId!);
        clock.Now = clock.Now.AddDays(7);
        Assert.Null((await Register("late@example.test", new(null, invitation.Code))).UserId);
        Assert.Equal(1, await Database.Users.CountAsync());
    }

    [Fact]
    public async Task FailedIdentityValidationDoesNotConsumeInvitation()
    {
        var owner = await Register("owner@example.test", new("Home", null));
        var invitation = await Service.InviteAsync(owner.UserId!);
        var result = await Service.RegisterAsync(new("bad@example.test", "weak", new(null, invitation.Code)));
        Assert.Null(result.UserId);
        Assert.False((await Database.HomeInvitations.SingleAsync()).Used);
        Assert.Equal(1, await Database.Users.CountAsync());
    }

    [Fact]
    public async Task ExistingAccountCanJoinOnlyOneHome()
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var legacy = new ApplicationUser { UserName = "legacy@example.test", Email = "legacy@example.test" };
        Assert.True((await users.CreateAsync(legacy, "StrongPassword123!")).Succeeded);
        Assert.Null(await Service.JoinAsync(legacy.Id, new("Existing user's home", null)));
        Assert.NotNull(await Service.JoinAsync(legacy.Id, new("Another home", null)));
        Assert.Single(await Database.Homes.ToListAsync());
        Assert.NotNull((await Service.InviteAsync("unknown-user")).Error);
    }

    [Fact]
    public async Task ExternalRegistrationAlsoRequiresHomeAndRollsBackOnInvalidCode()
    {
        var result = await Service.RegisterAsync(new("external@example.test", null, new(null, new string('B', 32)), new("Test", "key", "Test")));
        Assert.Null(result.UserId);
        Assert.Empty(await Database.Users.ToListAsync());
        Assert.Empty(await Database.UserLogins.ToListAsync());
        result = await Service.RegisterAsync(new("external@example.test", null, new("External home", null), new("Test", "key", "Test")));
        Assert.NotNull(result.UserId);
        Assert.Single(await Database.UserLogins.ToListAsync());
        Assert.True((await Service.GetAsync(result.UserId!))!.IsOwner);
    }

    [Fact]
    public async Task StaleInvitationRedemptionFailsConcurrencyCheck()
    {
        var owner = await Register("owner@example.test", new("Home", null));
        await Service.InviteAsync(owner.UserId!);
        var invitation = await Database.HomeInvitations.SingleAsync();
        await using var secondContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        var staleInvitation = await secondContext.HomeInvitations.SingleAsync();
        invitation.Redeem("first-member", clock.Now);
        await Database.SaveChangesAsync();
        staleInvitation.Redeem("second-member", clock.Now);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabasePreventsDeletingOwnerAndRemovesDeletedMemberMembership()
    {
        var owner = await Register("owner@example.test", new("Home", null));
        var invitation = await Service.InviteAsync(owner.UserId!);
        var member = await Register("member@example.test", new(null, invitation.Code));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.DeleteAsync((await users.FindByIdAsync(member.UserId!))!)).Succeeded);
        Assert.Null(await Service.GetAsync(member.UserId!));
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await users.DeleteAsync((await users.FindByIdAsync(owner.UserId!))!));
        Database.ChangeTracker.Clear();
        Assert.True((await Service.GetAsync(owner.UserId!))!.IsOwner);
    }

    [Fact]
    public void DomainEnforcesOwnerAndInvitationExpiry()
    {
        Assert.Throws<ArgumentException>(() => new Home(" ", "owner"));
        Assert.Throws<ArgumentException>(() => new Home(new string('x', 101), "owner"));
        var home = new Home("Home", "owner");
        Assert.Throws<UnauthorizedAccessException>(() => home.Invite("member", "hash", clock.Now));
        var invitation = home.Invite("owner", "hash", clock.Now);
        Assert.Throws<InvalidOperationException>(() => invitation.Redeem("member", clock.Now.AddDays(7)));
        invitation.Redeem("member", clock.Now);
        Assert.Throws<InvalidOperationException>(() => invitation.Redeem("other", clock.Now));
    }

    [Fact]
    public async Task OwnerCanConfigureHomeAndMembersCanOnlyRead()
    {
        var owner = (await Register("owner@example.test", new("Home", null))).UserId!;
        var invite = await Service.InviteAsync(owner);
        var member = (await Register("member@example.test", new(null, invite.Code))).UserId!;
        Assert.Null(await Service.RenameAsync(owner, "  New home  "));
        Assert.Null(await Service.AddFloorAsync(owner, " Ground floor "));
        var floor = Assert.Single((await Service.GetAsync(owner))!.Floors);
        Assert.Null(await Service.AddRoomAsync(owner, floor.Id, " Kitchen "));
        Database.ChangeTracker.Clear();
        var details = (await Service.GetAsync(member))!;
        Assert.Equal("New home", details.Name);
        Assert.Equal("Ground floor", Assert.Single(details.Floors).Name);
        Assert.Equal("Kitchen", Assert.Single(details.Floors[0].Rooms).Name);
        Assert.NotNull(await Service.RenameAsync(member, "No"));
        Assert.NotNull(await Service.AddFloorAsync(member, "No"));
        Assert.NotNull(await Service.AddRoomAsync(member, floor.Id, "No"));
        Assert.NotNull(await Service.RenameAsync("unknown", "No"));
        var other = (await Register("other@example.test", new("Other", null))).UserId!;
        Assert.NotNull(await Service.AddRoomAsync(other, floor.Id, "No"));
        Assert.NotNull(await Service.AddRoomAsync(owner, Guid.NewGuid(), "No"));
        Assert.Single(await Database.HomeRooms.ToListAsync());
        Assert.Single(await Database.HomeFloors.ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task InvalidConfigurationNamesDoNotPersist(string? name)
    {
        var owner = (await Register("owner@example.test", new("Home", null))).UserId!;
        await Service.AddFloorAsync(owner, "Floor");
        var floor = Assert.Single((await Service.GetAsync(owner))!.Floors);
        Assert.NotNull(await Service.RenameAsync(owner, name!));
        Assert.NotNull(await Service.AddFloorAsync(owner, name!));
        Assert.NotNull(await Service.AddRoomAsync(owner, floor.Id, name!));
        Assert.Equal("Home", (await Service.GetAsync(owner))!.Name);
        Assert.Single(await Database.HomeFloors.ToListAsync());
        Assert.Empty(await Database.HomeRooms.ToListAsync());
    }

    [Fact]
    public void DomainProtectsConfigurationAndFloorOwnership()
    {
        var home = new Home("Home", "owner");
        var floor = home.AddFloor("owner", "Floor");
        Assert.Throws<UnauthorizedAccessException>(() => home.Rename("member", "Name"));
        Assert.Throws<UnauthorizedAccessException>(() => home.AddFloor("member", "Floor"));
        Assert.Throws<UnauthorizedAccessException>(() => home.AddRoom("member", floor, "Room"));
        var foreignFloor = new Home("Other", "other").AddFloor("other", "Floor");
        Assert.Throws<ArgumentException>(() => home.AddRoom("owner", foreignFloor, "Room"));
        Assert.Throws<ArgumentException>(() => home.Rename("owner", " "));
        Assert.Throws<ArgumentException>(() => home.AddFloor("owner", new string('x', 101)));
        Assert.Throws<ArgumentException>(() => home.AddRoom("owner", floor, " "));
    }
    private Task<RegistrationResult> Register(string email, HomeChoice choice) =>
        Service.RegisterAsync(new(email, "StrongPassword123!", choice));

    [Fact]
    public async Task ProfilesPersistWithoutAccountsAndRetireWithoutLosingIdentity()
    {
        var owner = (await Register("owner@example.test", new("Home", null))).UserId!;
        Assert.Null(await Service.CreateProfileAsync(owner, "  Sam  ", "#abcdef"));
        var profile = Assert.Single((await Service.GetMembersAsync(owner))!.Members, member => !member.IsAccountLinked);
        Assert.Equal("Sam", profile.DisplayName);
        Assert.Equal("#ABCDEF", profile.Colour);
        Assert.Single(await Database.Users.ToListAsync());
        Assert.Null(await Service.EditProfileAsync(owner, profile.Id, "Samantha", null));
        Database.ChangeTracker.Clear();
        Assert.Equal("Samantha", (await Database.HomeMembers.SingleAsync(member => member.Id == profile.Id)).DisplayName);
        Assert.Null(await Service.RemoveMemberAsync(owner, profile.Id));
        Database.ChangeTracker.Clear();
        Assert.True((await Database.HomeMembers.SingleAsync(member => member.Id == profile.Id)).IsRetired);
        Assert.DoesNotContain((await Service.GetMembersAsync(owner))!.Members, member => member.Id == profile.Id);
        Assert.NotNull(await Service.EditProfileAsync(owner, profile.Id, "Restore", null));
    }

    [Fact]
    public async Task MemberRemovalRevokesAccessAndExplicitRejoinCreatesNewProfile()
    {
        var owner = (await Register("owner@example.test", new("Home", null))).UserId!;
        var code = (await Service.InviteAsync(owner)).Code;
        var member = (await Register("member@example.test", new(null, code))).UserId!;
        var memberId = (await Database.HomeMemberships.SingleAsync(link => link.UserId == member)).MemberId;
        var ownerId = (await Database.HomeMemberships.SingleAsync(link => link.UserId == owner)).MemberId;
        Assert.NotNull(await Service.RemoveMemberAsync(owner, ownerId));
        Assert.NotNull(await Service.RemoveMemberAsync(member, ownerId));
        Assert.NotNull(await Service.RemoveMemberAsync(member, memberId));
        Assert.Null(await Service.RemoveMemberAsync(owner, memberId));
        Database.ChangeTracker.Clear();
        Assert.Null(await Service.GetAsync(member));
        Assert.Null(await Service.GetMembersAsync(member));
        Assert.NotNull(await Service.RenameAsync(member, "Forbidden"));
        Assert.NotNull((await Service.InviteAsync(member)).Error);
        Assert.NotNull(await Service.CreateProfileAsync(member, "Forbidden", null));
        Assert.NotNull(await Service.EditProfileAsync(member, memberId, "Forbidden", null));
        Assert.Equal(2, await Database.Users.CountAsync());
        Assert.True((await Database.HomeMembers.SingleAsync(profile => profile.Id == memberId)).IsRetired);
        Assert.NotNull(await Service.JoinAsync(member, new(null, code)));
        Assert.Null(await Service.JoinAsync(member, new(null, (await Service.InviteAsync(owner)).Code)));
        Assert.NotEqual(memberId, (await Database.HomeMemberships.SingleAsync(link => link.UserId == member)).MemberId);
        Assert.Equal(3, await Database.HomeMembers.CountAsync());
    }

    [Fact]
    public async Task ManagementRejectsNonOwnersAndForeignIds()
    {
        var owner = (await Register("owner@example.test", new("Home", null))).UserId!;
        var member = (await Register("member@example.test", new(null, (await Service.InviteAsync(owner)).Code))).UserId!;
        var other = (await Register("other@example.test", new("Other", null))).UserId!;
        await Service.CreateProfileAsync(owner, "Child", null);
        var profile = (await Service.GetMembersAsync(owner))!.Members.Single(member => !member.IsAccountLinked);
        var invite = await Service.InviteAsync(owner);
        var inviteId = (await Service.GetInvitationsAsync(owner)).Single(invite => !invite.Used).Id;
        foreach (var actor in new[] { member, other, "unknown" })
        {
            Assert.NotNull(await Service.EditProfileAsync(actor, profile.Id, "Changed", null));
            Assert.NotNull(await Service.RemoveMemberAsync(actor, profile.Id));
            Assert.NotNull(await Service.RevokeInvitationAsync(actor, inviteId));
        }
        Assert.NotNull(await Service.CreateProfileAsync(member, "No", null));
        Assert.NotNull(await Service.ChangeTimeZoneAsync(member, "Europe/London"));
        Assert.Empty(await Service.GetInvitationsAsync(member));
        Assert.DoesNotContain((await Service.GetMembersAsync(other))!.Members, item => item.Id == profile.Id);
        Assert.Null(await Service.ChangeTimeZoneAsync(owner, "Europe/London"));
        Assert.Equal("Europe/London", (await Service.GetAsync(owner))!.TimeZoneId);
        Assert.NotNull(await Service.ChangeTimeZoneAsync(owner, "Not/AZone"));
        Assert.Equal("Europe/London", (await Service.GetAsync(owner))!.TimeZoneId);
        Assert.Null(await Service.RevokeInvitationAsync(owner, inviteId));
        Assert.Null((await Register("blocked@example.test", new(null, invite.Code))).UserId);
        Assert.NotNull(await Service.GetAsync(member));
        Assert.Equal(3, await Database.Users.CountAsync());
    }

    [Fact]
    public async Task RevocationWinsAgainstStaleRedemption()
    {
        var owner = (await Register("owner@example.test", new("Home", null))).UserId!;
        await Service.InviteAsync(owner);
        await using var stale = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        var invitation = await stale.HomeInvitations.SingleAsync();
        Assert.Null(await Service.RevokeInvitationAsync(owner, invitation.Id));
        invitation.Redeem("new-user", clock.Now);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }

    [Theory]
    [InlineData(" ", null)]
    [InlineData("Child", "red")]
    [InlineData("Child", "#12345G")]
    public async Task InvalidProfilesDoNotPersist(string name, string? colour)
    {
        var owner = (await Register("owner@example.test", new("Home", null))).UserId!;
        Assert.NotNull(await Service.CreateProfileAsync(owner, name, colour));
        Assert.Single(await Database.HomeMembers.ToListAsync());
    }

    [Fact]
    public async Task RepeatedRegistrationDoesNotCreateDuplicateHomeOrProfile()
    {
        Assert.NotNull((await Register("owner@example.test", new("Home", null))).UserId);
        Assert.Null((await Register("owner@example.test", new("Duplicate", null))).UserId);
        Assert.Single(await Database.Homes.ToListAsync());
        Assert.Single(await Database.HomeMembers.ToListAsync());
        Assert.Single(await Database.HomeMemberships.ToListAsync());
    }

    [Fact]
    public void DomainProtectsProfilesOwnerRemovalAndRevocation()
    {
        var home = new Home("Home", "owner");
        var profile = home.CreateProfile("owner", "Child", null);
        Assert.Throws<UnauthorizedAccessException>(() => home.CreateProfile("member", "Child", null));
        Assert.Throws<UnauthorizedAccessException>(() => home.EditProfile("member", profile, "Changed", null));
        Assert.Throws<UnauthorizedAccessException>(() => home.RemoveMember("member", profile, null));
        var ownerLink = new HomeMembership(home.Id, "owner");
        var ownerProfile = new HomeMember(ownerLink, "Owner");
        Assert.Throws<InvalidOperationException>(() => home.RemoveMember("owner", ownerProfile, ownerLink));
        var otherHome = new Home("Other", "other");
        Assert.Throws<ArgumentException>(() => home.RemoveMember("owner", otherHome.CreateProfile("other", "Other", null), null));
        Assert.Throws<UnauthorizedAccessException>(() => home.ChangeTimeZone("member", "UTC"));
        var invitation = home.Invite("owner", "hash", clock.Now);
        Assert.Throws<UnauthorizedAccessException>(() => home.RevokeInvitation("member", invitation));
        home.RevokeInvitation("owner", invitation);
        Assert.False(invitation.IsAvailable(clock.Now));
        Assert.Throws<InvalidOperationException>(() => invitation.Redeem("member", clock.Now));
    }

    public void Dispose()
    {
        scope.Dispose();
        provider.Dispose();
        connection.Dispose();
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
