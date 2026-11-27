using System.Security.Cryptography;
using System.Text;

namespace SameieHub.Web.Hosting;

/// <summary>
/// The only page that can be opened without a session. It is self-contained: no outside scripts, fonts, styles or images, and a
/// content security policy that allows exactly the one inline script and style below. The code arrives in the part of the address
/// after the #, which the browser never sends to the program, never puts in a Referer header, and which the script removes from the
/// address at once. The script then posts the code to the program, which answers with the session cookie.
/// </summary>
internal static class StartPage
{
    public const string Path = "/start";

    private const string Style = "body{font-family:system-ui,sans-serif;margin:3rem;color:#222}";

    private const string Script =
        "(function(){var m=document.getElementById('m');"
        + "function fail(){m.textContent='Tilgangskoden er ugyldig eller utløpt. Start SameieHub på nytt (SameieHub.exe) for å få en ny.';}"
        + "var code=location.hash.slice(1);history.replaceState(null,'',location.pathname);"
        + "if(!code){fail();return;}"
        + "fetch('/start',{method:'POST',headers:{'Content-Type':'text/plain'},body:code,credentials:'same-origin',cache:'no-store'})"
        + ".then(function(r){if(r.ok){location.replace('/');}else{fail();}},fail);})();";

    public static readonly string Html =
        "<!doctype html><html lang=\"nb\"><head><meta charset=\"utf-8\"><meta name=\"referrer\" content=\"no-referrer\">"
        + "<title>SameieHub</title><link rel=\"icon\" href=\"data:,\">"
        + "<style>" + Style + "</style></head><body><h1>SameieHub</h1><p id=\"m\">Åpner SameieHub …</p>"
        + "<noscript>Nettleseren må tillate JavaScript for at SameieHub skal kunne åpnes.</noscript>"
        + "<script>" + Script + "</script></body></html>";

    public static readonly string ContentSecurityPolicy =
        "default-src 'none'; script-src " + HashOf(Script) + "; style-src " + HashOf(Style)
        + "; connect-src 'self'; img-src data:; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    /// <summary>The value a content security policy needs to allow exactly this inline text.</summary>
    public static string HashOf(string text)
    {
        return "'sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(text))) + "'";
    }
}
