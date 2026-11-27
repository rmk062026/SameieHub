using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Infrastructure;
using SameieHub.Tests.Fakes;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

/// <summary>
/// Starts the real components and pipeline in Windows mode (<see cref="WebAppSetup"/>) on a loopback port with a temporary database,
/// and talks to it the way a browser, another web site or another program on the computer would. No cookies or redirects are
/// handled automatically, so every request says exactly what it brings.
/// </summary>
public sealed class GuardedAppFixture : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SameieHubTests", Guid.NewGuid().ToString());

    public ManualTimeProvider Clock { get; } = new(new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc));

    public WebApplication App { get; private set; } = null!;

    public string Address { get; private set; } = string.Empty;

    public int Port => new Uri(Address).Port;

    internal AccessSession Session => App.Services.GetRequiredService<AccessSession>();

    public string LogFolder => Path.Combine(_folder, "logs");

    public HttpClient Client { get; } = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_folder);
        var databasePath = Path.Combine(_folder, "sameiehub.db");
        var builder = WindowsHost.CreateBuilder([$"--Database:Path={databasePath}"], AppContext.BaseDirectory);
        // The static file list is found by the name of the program, which inside the test host would be the test host's.
        builder.Environment.ApplicationName = "SameieHub";
        WebAppSetup.AddServices(builder, databasePath, windowsHost: true);
        builder.Services.AddSingleton<TimeProvider>(Clock);
        App = builder.Build();
        await App.Services.MigrateSameieHubDatabaseAsync();
        WebAppSetup.UsePipeline(App, windowsHost: true);
        await App.StartAsync();
        Address = WindowsHost.GetAddress(App)!;
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    public string CookieName => $"SameieHub-{Port}";

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? cookie = null, Action<HttpRequestMessage>? configure = null, string? body = null)
    {
        using var request = new HttpRequestMessage(method, Address + path);
        if (cookie != null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (body != null)
        {
            request.Content = new StringContent(body);
        }

        configure?.Invoke(request);

        return await Client.SendAsync(request);
    }

    /// <summary>Redeems a fresh launch code the way the start page does, and returns the cookie a browser would send back.</summary>
    public async Task<(string Cookie, string Code, string Token)> LoginAsync()
    {
        var code = Session.CreateLaunchCode();
        using var response = await SendAsync(HttpMethod.Post, "/start", body: code);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        var token = setCookie.Split(';')[0].Split('=', 2)[1];

        return ($"{CookieName}={token}", code, token);
    }

    public string ReadAllLogText()
    {
        return Directory.Exists(LogFolder) ? string.Concat(Directory.GetFiles(LogFolder, "*.log").Select(File.ReadAllText)) : string.Empty;
    }
}

public class AccessGuardTests : IClassFixture<GuardedAppFixture>
{
    private readonly GuardedAppFixture _app;

    public AccessGuardTests(GuardedAppFixture app)
    {
        _app = app;
    }

    // ---- without a session: nothing is available except the start page

    [Theory]
    [InlineData("/")]
    [InlineData("/oppgaver")]
    [InlineData("/boenheter/ny")]
    [InlineData("/boenheter/1")]
    [InlineData("/styresaker/1/rediger")]
    [InlineData("/innstillinger/sikkerhetskopi")]
    [InlineData("/elbillading")]
    [InlineData("/favicon.png")]
    [InlineData("/app.css")]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/_blazor")]
    [InlineData("/_blazor/initializers")]
    [InlineData("/does-not-exist")]
    [InlineData("/Error")]
    [InlineData("/start/extra")]
    public async Task WithoutASession_EveryAddressIsRefused_IncludingFilesAndInternalAddresses(string path)
    {
        using var response = await _app.SendAsync(HttpMethod.Get, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Oppgaver", text);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("POST", "/boenheter/ny")]
    [InlineData("POST", "/")]
    [InlineData("POST", "/_blazor/negotiate?negotiateVersion=1")]
    [InlineData("PUT", "/boenheter/1")]
    [InlineData("DELETE", "/boenheter/1")]
    [InlineData("POST", "/innstillinger/sikkerhetskopi")]
    public async Task WithoutASession_ADataChangingRequestIsRefused_BeforeAnythingElseLooksAtIt(string method, string path)
    {
        using var response = await _app.SendAsync(new HttpMethod(method), path, body: "Title=Fiktiv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WithoutASession_TheBlazorWebSocketIsRefused()
    {
        using var socket = new ClientWebSocket();

        var error = await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(new Uri($"ws://127.0.0.1:{_app.Port}/_blazor?id=anything"), CancellationToken.None));

        Assert.Contains("403", error.Message);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    public async Task AnInvalidCookie_IsRefused(string value)
    {
        using var response = await _app.SendAsync(HttpMethod.Get, "/", cookie: $"{_app.CookieName}={value}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ACookieForAnotherPort_IsNotAccepted_EvenWhenItHoldsAValidToken()
    {
        var (_, _, token) = await _app.LoginAsync();

        using var response = await _app.SendAsync(HttpMethod.Get, "/", cookie: $"SameieHub-{_app.Port + 1}={token}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ACookieThatHasExpired_IsRefused()
    {
        var (cookie, _, _) = await _app.LoginAsync();
        using (var fine = await _app.SendAsync(HttpMethod.Get, "/", cookie))
        {
            Assert.Equal(HttpStatusCode.OK, fine.StatusCode);
        }

        _app.Clock.Advance(TimeSpan.FromHours(9));

        using var response = await _app.SendAsync(HttpMethod.Get, "/", cookie);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ASessionFromAnotherRunOfTheProgram_IsNotAccepted()
    {
        await using var other = new GuardedAppFixture();
        await other.InitializeAsync();
        var (_, _, otherToken) = await other.LoginAsync();

        using var response = await _app.SendAsync(HttpMethod.Get, "/", cookie: $"{_app.CookieName}={otherToken}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- the start page and the one-time code

    [Fact]
    public async Task TheStartPage_IsAvailableWithoutASession_AndLoadsNothingFromOutsideItself()
    {
        using var response = await _app.SendAsync(HttpMethod.Get, "/start");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'none'", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.Matches("script-src 'sha256-[A-Za-z0-9+/=]+'", policy);
        Assert.DoesNotMatch("(?i)(src|href)\\s*=\\s*[\"']?(https?:)?//", html);
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
        Assert.Contains("location.hash", html);
        Assert.Contains("history.replaceState", html);
    }

    [Fact]
    public async Task TheStartPage_PolicyAllowsExactlyTheInlineScriptAndStyleItContains()
    {
        using var response = await _app.SendAsync(HttpMethod.Get, "/start");
        var html = await response.Content.ReadAsStringAsync();
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();

        var script = System.Text.RegularExpressions.Regex.Match(html, "<script>(.*?)</script>", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
        var style = System.Text.RegularExpressions.Regex.Match(html, "<style>(.*?)</style>", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;

        Assert.Contains(StartPage.HashOf(script), policy);
        Assert.Contains(StartPage.HashOf(style), policy);
    }

    [Fact]
    public async Task ACorrectCode_GivesAnHttpOnlyStrictSessionCookieForThisHostOnly()
    {
        var code = _app.Session.CreateLaunchCode();

        using var response = await _app.SendAsync(HttpMethod.Post, "/start", body: code);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.StartsWith(_app.CookieName + "=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age=", cookie, StringComparison.OrdinalIgnoreCase);
        // Secure is left off on purpose: the program only has plain http on 127.0.0.1, where a Secure cookie may not be sent back.
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.False(cookie.Contains(code), "The cookie must not contain the launch code.");
    }

    [Fact]
    public async Task ACodeWorksOnce()
    {
        var code = _app.Session.CreateLaunchCode();

        using var first = await _app.SendAsync(HttpMethod.Post, "/start", body: code);
        using var second = await _app.SendAsync(HttpMethod.Post, "/start", body: code);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
        Assert.False(second.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task AnExpiredCode_AWrongCode_AndNoCode_AreRefused_WithoutACookie()
    {
        var expired = _app.Session.CreateLaunchCode();
        _app.Clock.Advance(TimeSpan.FromSeconds(61));

        foreach (var body in new[] { expired, "wrong", string.Empty, new string('A', 5000) })
        {
            using var response = await _app.SendAsync(HttpMethod.Post, "/start", body: body);

            Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.RequestEntityTooLarge, response.StatusCode.ToString());
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
    }

    [Fact]
    public async Task TheCodeInTheAddressIsNotUsed_OnlyAPostCanRedeemIt()
    {
        var code = _app.Session.CreateLaunchCode();

        using var inQuery = await _app.SendAsync(HttpMethod.Get, $"/start?code={code}");
        using var inFragmentLikePath = await _app.SendAsync(HttpMethod.Get, $"/start/{code}");

        Assert.False(inQuery.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Forbidden, inFragmentLikePath.StatusCode);
        Assert.True(_app.Session.TryRedeemLaunchCode(code, out _), "A GET must not use up the code.");
    }

    [Fact]
    public async Task ARedeemFromAnotherOrigin_IsRefused_AndDoesNotUseUpTheCode()
    {
        var code = _app.Session.CreateLaunchCode();

        using var response = await _app.SendAsync(HttpMethod.Post, "/start", body: code, configure: request => request.Headers.Add("Origin", "https://evil.example"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.True(_app.Session.TryRedeemLaunchCode(code, out _));
    }

    [Fact]
    public async Task OtherMethodsOnTheStartAddress_AreNotAllowed()
    {
        using var put = await _app.SendAsync(HttpMethod.Put, "/start", body: "x");
        using var delete = await _app.SendAsync(HttpMethod.Delete, "/start");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }

    // ---- with a session: the program works as before

    [Theory]
    [InlineData("/")]
    [InlineData("/oppgaver")]
    [InlineData("/styresaker")]
    [InlineData("/boenheter")]
    [InlineData("/boenheter/ny")]
    [InlineData("/oppgaver/ny")]
    [InlineData("/elbillading")]
    [InlineData("/innstillinger/sikkerhetskopi")]
    public async Task WithAValidSession_ThePagesAreServed(string path)
    {
        var (cookie, _, _) = await _app.LoginAsync();

        using var response = await _app.SendAsync(HttpMethod.Get, path, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("SameieHub", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithAValidSession_TheBlazorConnectionCanBeMade_OverNegotiateAndWebSocket()
    {
        var (cookie, _, _) = await _app.LoginAsync();
        var origin = _app.Address;

        using var negotiate = await _app.SendAsync(HttpMethod.Post, "/_blazor/negotiate?negotiateVersion=1", cookie, request => request.Headers.Add("Origin", origin));
        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
        var token = JsonDocument.Parse(await negotiate.Content.ReadAsStringAsync()).RootElement.GetProperty("connectionToken").GetString();

        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Cookie", cookie);
        socket.Options.SetRequestHeader("Origin", origin);
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{_app.Port}/_blazor?id={token}"), CancellationToken.None);

        Assert.Equal(WebSocketState.Open, socket.State);
    }

    [Fact]
    public async Task WithAValidSession_AWebSocketFromAnotherOriginIsStillRefused()
    {
        var (cookie, _, _) = await _app.LoginAsync();
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Cookie", cookie);
        socket.Options.SetRequestHeader("Origin", "https://evil.example");

        var error = await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(new Uri($"ws://127.0.0.1:{_app.Port}/_blazor?id=anything"), CancellationToken.None));

        Assert.Contains("403", error.Message);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://localhost:1")]
    [InlineData("http://127.0.0.1:1")]
    [InlineData("null")]
    public async Task WithAValidSession_ARequestFromAnotherOriginIsRefused(string origin)
    {
        var (cookie, _, _) = await _app.LoginAsync();

        using var response = await _app.SendAsync(HttpMethod.Post, "/_blazor/negotiate?negotiateVersion=1", cookie, request => request.Headers.Add("Origin", origin));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("cross-site", HttpStatusCode.Forbidden)]
    [InlineData("same-site", HttpStatusCode.Forbidden)]
    [InlineData("same-origin", HttpStatusCode.OK)]
    [InlineData("none", HttpStatusCode.OK)]
    public async Task TheBrowsersOwnSiteHeader_DecidesWhetherARequestMayUseTheSession(string site, HttpStatusCode expected)
    {
        var (cookie, _, _) = await _app.LoginAsync();

        using var response = await _app.SendAsync(HttpMethod.Get, "/oppgaver", cookie, request => request.Headers.Add("Sec-Fetch-Site", site));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task AForeignHostName_IsRefusedEvenWithAValidSession()
    {
        var (cookie, _, _) = await _app.LoginAsync();

        using var response = await _app.SendAsync(HttpMethod.Get, "/", cookie, request => request.Headers.Host = "evil.example");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EveryResponseCarriesTheSecurityHeaders()
    {
        var (cookie, _, _) = await _app.LoginAsync();

        foreach (var cookieToSend in new string?[] { null, cookie })
        {
            using var response = await _app.SendAsync(HttpMethod.Get, "/", cookieToSend);

            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        }
    }

    [Fact]
    public async Task TheLog_NeverContainsACodeOrASessionToken()
    {
        var (cookie, code, token) = await _app.LoginAsync();
        using (await _app.SendAsync(HttpMethod.Get, "/", cookie))
        {
        }

        using (await _app.SendAsync(HttpMethod.Get, "/", cookie: "x=y"))
        {
        }

        var log = _app.ReadAllLogText();

        Assert.DoesNotContain(code, log);
        Assert.DoesNotContain(token, log);
        Assert.DoesNotContain("Set-Cookie", log, StringComparison.OrdinalIgnoreCase);
    }

    // ---- the development start is not guarded

    [Fact]
    public async Task TheDevelopmentStart_HasNoGuard_AndNoStartAddress()
    {
        var folder = Path.Combine(Path.GetTempPath(), "SameieHubTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(folder);
        try
        {
            var databasePath = Path.Combine(folder, "sameiehub.db");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory, ApplicationName = "SameieHub" });
            builder.Configuration["urls"] = "http://127.0.0.1:0";
            WebAppSetup.AddServices(builder, databasePath, windowsHost: false);
            await using var app = builder.Build();
            await app.Services.MigrateSameieHubDatabaseAsync();
            WebAppSetup.UsePipeline(app, windowsHost: false);
            await app.StartAsync();
            var address = WindowsHost.GetAddress(app)!;
            using var client = new HttpClient();

            using var front = await client.GetAsync(address + "/oppgaver");
            using var start = await client.GetAsync(address + "/start");

            Assert.Equal(HttpStatusCode.OK, front.StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, start.StatusCode);
            Assert.Null(app.Services.GetService<AccessSession>());
            SqliteConnection.ClearAllPools();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
