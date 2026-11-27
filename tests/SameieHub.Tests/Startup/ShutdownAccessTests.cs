using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

/// <summary>
/// The program can be stopped from the web interface only through the page's own connection, which the access guard already protects.
/// There is no web address that stops it, so no request, from any site or program, can do it by guessing one.
/// </summary>
public class ShutdownAccessTests : IClassFixture<GuardedAppFixture>
{
    private readonly GuardedAppFixture _app;

    public ShutdownAccessTests(GuardedAppFixture app)
    {
        _app = app;
    }

    private ShutdownService Shutdown => _app.App.Services.GetRequiredService<ShutdownService>();

    private bool IsStopping => _app.App.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested;

    [Theory]
    [InlineData("/avslutt")]
    [InlineData("/avslutt-og-start-paa-nytt")]
    [InlineData("/shutdown")]
    [InlineData("/stop")]
    [InlineData("/restart")]
    [InlineData("/quit")]
    [InlineData("/exit")]
    [InlineData("/api/shutdown")]
    [InlineData("/_shutdown")]
    [InlineData("/om/avslutt")]
    [InlineData("/start/stop")]
    public async Task WithoutASession_NoAddressStopsTheProgram_WhateverTheMethod(string path)
    {
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            using var response = await _app.SendAsync(method, path, body: "restart=true");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.False(Shutdown.IsRequested);
        Assert.False(IsStopping);
    }

    [Theory]
    [InlineData("/avslutt")]
    [InlineData("/avslutt-og-start-paa-nytt")]
    [InlineData("/shutdown")]
    [InlineData("/stop")]
    [InlineData("/restart")]
    [InlineData("/api/shutdown")]
    [InlineData("/_shutdown")]
    public async Task EvenWithAValidSession_NoAddressStopsTheProgram_ASimpleRequestOrALinkCannotDoIt(string path)
    {
        var (cookie, _, _) = await _app.LoginAsync();

        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            using var response = await _app.SendAsync(method, path, cookie, body: "restart=true");

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.False(Shutdown.IsRequested);
        Assert.False(IsStopping);
    }

    [Fact]
    public async Task APageOnAnotherSite_CannotReachTheProgramEvenWithTheCookie()
    {
        var (cookie, _, _) = await _app.LoginAsync();

        using var response = await _app.SendAsync(HttpMethod.Post, "/avslutt", cookie, request =>
        {
            request.Headers.Add("Origin", "http://evil.example");
            request.Headers.Add("Sec-Fetch-Site", "cross-site");
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(Shutdown.IsRequested);
    }

    [Fact]
    public async Task TheAboutPage_IsOnlyReachableWithAValidSession_LikeEveryOtherPage()
    {
        var (cookie, _, _) = await _app.LoginAsync();

        using var about = await _app.SendAsync(HttpMethod.Get, "/om", cookie);
        using var aboutWithoutSession = await _app.SendAsync(HttpMethod.Get, "/om");

        Assert.Equal(HttpStatusCode.OK, about.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, aboutWithoutSession.StatusCode);
        Assert.False(Shutdown.IsRequested);
    }

    [Fact]
    public void TheProgramOffersAShutdown_OnlyBecauseItRunsInWindowsMode()
    {
        Assert.True(Shutdown.CanShutDown);
    }
}
