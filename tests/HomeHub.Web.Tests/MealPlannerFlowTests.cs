using System.Net;
using HomeHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed partial class RegistrationFlowTests
{
    [Fact]
    public async Task MealFormsPersistSelectionsRenameAndConfirmDeletion()
    {
        using var factory = new RegistrationFactory();
        using var client = factory.Client();
        await Register(client, "meals@example.test", "Meal home", "");
        await SignIn(factory, client, "meals@example.test");
        var empty = await client.GetStringAsync("/MealPlanner");
        Assert.Contains("Monday", empty);
        Assert.Contains("Friday", empty);
        Assert.DoesNotContain("Saturday", empty);
        Assert.Contains("Add your first meal", empty);
        Assert.Contains("app-sidebar", empty);
        var created = await Post(client, "/Meals/New", "save-meal", new() { ["Input.Name"] = "Pasta" });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Guid mealId;
        Guid memberId;
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            mealId = (await database.Meals.SingleAsync()).Id;
            memberId = (await database.HomeMembers.SingleAsync()).Id;
        }
        var added = await Post(client, "/MealPlanner", "plan-meal", new()
        {
            ["Input.MealId"] = mealId.ToString(), ["Input.Day"] = "Monday"
        });
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        Guid entryId;
        using (var scope = factory.Services.CreateScope())
            entryId = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PlannedMeals.SingleAsync()).Id;
        Assert.Contains("Everyone", await client.GetStringAsync("/MealPlanner"));
        var entryPath = $"/MealPlanner/Entry/{entryId}";
        var selected = await Post(client, entryPath, "meal-members", new()
        {
            ["Input.Choices[0].Id"] = memberId.ToString(),
            ["Input.Choices[0].Selected"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, selected.StatusCode);
        using (var scope = factory.Services.CreateScope())
            Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<HomeHub.Domain.Meals.PlannedMealMember>().ToListAsync());
        var cleared = await Post(client, entryPath, "meal-members", new()
        {
            ["Input.Choices[0].Id"] = memberId.ToString(),
            ["Input.Choices[0].Selected"] = "false"
        });
        Assert.Equal(HttpStatusCode.Redirect, cleared.StatusCode);
        Assert.Contains("Everyone", await client.GetStringAsync("/MealPlanner"));
        var renamed = await Post(client, $"/Meals/Edit/{mealId}", "save-meal", new() { ["Input.Name"] = "Noodles" });
        Assert.Equal(HttpStatusCode.Redirect, renamed.StatusCode);
        Assert.Contains("Noodles", await client.GetStringAsync("/MealPlanner"));
        var deletePath = $"/Meals?delete={mealId}";
        var warning = await client.GetStringAsync(deletePath);
        Assert.Contains("<dialog", warning);
        Assert.Contains("planned on Monday", warning);
        Assert.Contains("Cancel", warning);
        // Opening the confirmation and navigating away must not delete anything.
        Assert.Contains("Noodles", await client.GetStringAsync("/Meals"));
        var removed = await Post(client, entryPath, "remove-planned-meal", new());
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Contains("Noodles", await client.GetStringAsync("/Meals"));
        await Post(client, "/MealPlanner", "plan-meal", new()
        {
            ["Input.MealId"] = mealId.ToString(), ["Input.Day"] = "Friday"
        });
        var deleted = await Post(client, deletePath, "delete-meal", new() { ["Delete.Id"] = mealId.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await database.Meals.ToListAsync());
            Assert.Empty(await database.PlannedMeals.ToListAsync());
        }
    }

    [Fact]
    public async Task MealFormsRejectMissingAntiforgeryAndInvalidInputs()
    {
        using var factory = new RegistrationFactory();
        using var client = factory.Client();
        await Register(client, "validation@example.test", "Home", "");
        await SignIn(factory, client, "validation@example.test");
        var invalid = await Post(client, "/Meals/New", "save-meal", new() { ["Input.Name"] = " " });
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var missingToken = await client.PostAsync("/Meals/New", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "save-meal", ["Input.Name"] = "Unprotected"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Meals.ToListAsync());
    }
}
