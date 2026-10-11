using System.Net;
using Xunit;

namespace HomeHub.Web.Tests;

public sealed class AccountFlowTests
{
    [Fact]
    public async Task LoginLogoutAndPasswordChangeWorkThroughStaticForms()
    {
        using var app = new RestartFactory(); await app.InitializeAsync();
        using var browser = app.Browser();
        var created = await BrowserForms.PostAsync(browser, "/Account/Register", "register", new()
        {
            ["Input.Email"] = "account@example.test", ["Input.Name"] = "Account member",
            ["Input.Password"] = "HomeHub!123", ["Input.ConfirmPassword"] = "HomeHub!123",
            ["Input.Choice"] = "Create", ["Input.HouseholdName"] = "Account family"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var rejectedDownload = await browser.PostAsync("/Account/Manage/DownloadPersonalData", new StringContent(""));
        Assert.Equal(HttpStatusCode.BadRequest, rejectedDownload.StatusCode);
        var download = await BrowserForms.PostAsync(browser, "/Account/Manage/DownloadPersonalData", "", new(),
            tokenPage: "/Account/Manage/PersonalData");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Contains("account@example.test", await download.Content.ReadAsStringAsync());
        var changed = await BrowserForms.PostAsync(browser, "/Account/Manage/ChangePassword", "change-password", new()
        {
            ["Input.OldPassword"] = "HomeHub!123", ["Input.NewPassword"] = "Changed!123", ["Input.ConfirmPassword"] = "Changed!123"
        });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        var logout = await BrowserForms.PostAsync(browser, "/Account/Logout", "", new() { ["returnUrl"] = "" }, tokenPage: "/Account/Manage");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Dashboard")).StatusCode);
        var login = await BrowserForms.PostAsync(browser, "/Account/Login", "login", new()
        {
            ["Input.Email"] = "account@example.test", ["Input.Password"] = "Changed!123", ["Input.RememberMe"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Dashboard")).StatusCode);
    }
}
