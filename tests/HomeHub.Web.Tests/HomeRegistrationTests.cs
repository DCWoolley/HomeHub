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

    private Task<RegistrationResult> Register(string email, HomeChoice choice) =>
        Service.RegisterAsync(new(email, "StrongPassword123!", choice));

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
