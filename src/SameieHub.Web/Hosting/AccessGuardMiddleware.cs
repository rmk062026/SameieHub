using System.Text;

namespace SameieHub.Web.Hosting;

/// <summary>
/// In Windows mode every request must come from the browser that was given the session cookie, with one exception: the start
/// address (<see cref="StartPage.Path"/>), which serves the start page and redeems a launch code. There is no other list of
/// exceptions: pages, static files, the Blazor connection (negotiate and WebSocket), form posts and every address that does not
/// exist are all refused without the cookie. The guard runs first, before routing, so no endpoint, now or later, can be reached around it.
/// </summary>
/// <remarks>
/// Two further checks apply to everything, including the Blazor WebSocket: an <c>Origin</c> header must be this program's own
/// address, and a <c>Sec-Fetch-Site</c> header must say same-origin or none. A page on another local port counts as the same
/// site for SameSite cookies, so the cookie alone would not stop it. The host name is checked earlier, by host filtering.
/// </remarks>
internal sealed class AccessGuardMiddleware
{
    private const int MaxCodeBytes = 256;

    private readonly RequestDelegate _next;
    private readonly AccessSession _session;

    public AccessGuardMiddleware(RequestDelegate next, AccessSession session)
    {
        _next = next;
        _session = session;
    }

    public static string CookieName(HttpContext context)
    {
        // A cookie belongs to the host name, not the port, so two SameieHub programs on this computer would otherwise share one name.
        return $"SameieHub-{context.Connection.LocalPort}";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        AddSecurityHeaders(context.Response);

        if (context.Request.Path == StartPage.Path)
        {
            await HandleStartAddressAsync(context);

            return;
        }

        if (!IsFromOwnOrigin(context.Request) || !_session.IsValidSession(context.Request.Cookies[CookieName(context)]))
        {
            await RefuseAsync(context);

            return;
        }

        await _next(context);
    }

    private static void AddSecurityHeaders(HttpResponse response)
    {
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["X-Frame-Options"] = "DENY";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    }

    private static bool IsFromOwnOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (origin.Length > 0 && !string.Equals(origin, $"http://{request.Host}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var site = request.Headers["Sec-Fetch-Site"].ToString();

        return site.Length == 0 || site is "same-origin" or "none";
    }

    private async Task HandleStartAddressAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.ContentSecurityPolicy = StartPage.ContentSecurityPolicy;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(StartPage.Html);

            return;
        }

        if (!HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers.Allow = "GET, POST";

            return;
        }

        await RedeemAsync(context);
    }

    private async Task RedeemAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!IsFromOwnOrigin(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            return;
        }

        var code = await ReadCodeAsync(context.Request);
        if (code == null)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;

            return;
        }

        if (!_session.TryRedeemLaunchCode(code, out var sessionToken))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            return;
        }

        // Secure is left off on purpose: there is only plain http on 127.0.0.1, where a Secure cookie may not be sent back by every
        // browser. The cookie has no expiry, so the browser drops it when it closes, and the program drops it after SessionLifetime.
        context.Response.Cookies.Append(CookieName(context), sessionToken, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Secure = false,
            IsEssential = true
        });
        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    /// <summary>Reads the body as text, or returns null when it is longer than any code can be.</summary>
    private static async Task<string?> ReadCodeAsync(HttpRequest request)
    {
        var buffer = new byte[MaxCodeBytes + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await request.Body.ReadAsync(buffer.AsMemory(length));
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        return length > MaxCodeBytes ? null : Encoding.UTF8.GetString(buffer, 0, length);
    }

    private static async Task RefuseAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(
            "<!doctype html><meta charset=\"utf-8\"><title>Ingen tilgang</title>"
            + "<h1>Ingen tilgang</h1><p>Denne adressen er bare for SameieHub-programmet på denne maskinen. "
            + "Start SameieHub (SameieHub.exe) for å åpne det i nettleseren.</p>");
    }
}
