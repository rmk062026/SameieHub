using System.Reflection;

namespace SameieHub.Web.Hosting;

/// <summary>The installed version, read from the program's own version information (set by the build, for example <c>-p:Version=0.8.5</c>).</summary>
internal static class AppVersion
{
    public static string Current => Describe(typeof(AppVersion).Assembly);

    public static string Describe(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var fallback = assembly.GetName().Version?.ToString(3) ?? "ukjent";

        return Clean(informational, fallback);
    }

    /// <summary>The build adds the commit after a plus sign (<c>0.8.5+3f2a9c</c>). The person only needs <c>0.8.5</c>.</summary>
    public static string Clean(string? informationalVersion, string fallback)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return fallback;
        }

        var plus = informationalVersion.IndexOf('+');
        var version = (plus >= 0 ? informationalVersion[..plus] : informationalVersion).Trim();

        return version.Length > 0 ? version : fallback;
    }
}
