using System.Net;
using System.Text.RegularExpressions;

namespace HomeHub.Web.Tests;

public static class BrowserForms
{
    public static async Task<HttpResponseMessage> PostAsync(HttpClient browser, string path, string handler, Dictionary<string, string> input, string? tokenPage = null)
    {
        var html = await browser.GetStringAsync(tokenPage ?? path);
        var field = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!field.Success) throw new InvalidOperationException("Antiforgery token missing.");
        input["__RequestVerificationToken"] = WebUtility.HtmlDecode(field.Groups[1].Value);
        input["_handler"] = handler;
        return await browser.PostAsync(path, new FormUrlEncodedContent(input));
    }
}
