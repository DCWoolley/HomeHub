using System.Net;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class RegistrationHttpTests
{
    [Fact]
    public async Task RegistrationPostCreatesCookieAndRequiresHouseholdChoice()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        using var browser = app.Browser();
        var input = new Dictionary<string, string>
        {
            ["Input.Email"] = "http@example.test", ["Input.Name"] = "HTTP member",
            ["Input.Password"] = "HomeHub!123", ["Input.ConfirmPassword"] = "HomeHub!123",
            ["Input.Choice"] = "Create", ["Input.HouseholdName"] = "HTTP family"
        };
        var response = await BrowserForms.PostAsync(browser, "/Account/Register", "register", input);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Household", response.Headers.Location!.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains(".AspNetCore.Identity.Application"));
    }

    [Fact]
    public async Task RegistrationPostWithoutAntiforgeryIsRejected()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        using var browser = app.Browser();
        var response = await browser.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string> { ["_handler"] = "register" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
