using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace SameieHub.Web.Hosting;

/// <summary>
/// The secrets that decide who may use the program in Windows mode, kept in memory only. A launch code is a one-time secret that
/// the program gives to the browser it opens itself; the browser swaps it for a session token (the cookie). Nothing is written
/// to disk and nothing survives a restart, so a new run of the program never accepts an old code or cookie.
/// </summary>
/// <remarks>
/// Only a hash of each secret is kept, and a presented secret is compared with every stored hash in fixed time, without stopping
/// at the first match. ASP.NET Core's cookie authentication was not used: it needs data protection keys that are saved on disk, which
/// would let a cookie outlive the program, and it has no one-time code.
/// </remarks>
internal sealed class AccessSession
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    private const int SecretBytes = 32;
    private const int MaxPresentedLength = 128;
    private const int MaxUnusedCodes = 20;
    private const int MaxSessions = 50;

    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private readonly List<Entry> _codes = [];
    private readonly List<Entry> _sessions = [];

    public AccessSession(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    private readonly record struct Entry(byte[] Hash, DateTimeOffset ExpiresAt);

    /// <summary>A new code of 256 random bits, valid for <see cref="CodeLifetime"/> and for one use.</summary>
    public string CreateLaunchCode()
    {
        var code = NewSecret();
        lock (_lock)
        {
            RemoveExpired(_codes);
            if (_codes.Count >= MaxUnusedCodes)
            {
                _codes.RemoveAt(0);
            }

            _codes.Add(new Entry(HashOf(code), _timeProvider.GetUtcNow() + CodeLifetime));
        }

        return code;
    }

    /// <summary>Uses up the code and gives a new session token, or returns false and an empty token.</summary>
    public bool TryRedeemLaunchCode(string? code, out string sessionToken)
    {
        sessionToken = string.Empty;
        lock (_lock)
        {
            RemoveExpired(_codes);
            var index = IndexOfMatch(_codes, code);
            if (index < 0)
            {
                return false;
            }

            _codes.RemoveAt(index);
            sessionToken = NewSecret();
            RemoveExpired(_sessions);
            if (_sessions.Count >= MaxSessions)
            {
                _sessions.RemoveAt(0);
            }

            _sessions.Add(new Entry(HashOf(sessionToken), _timeProvider.GetUtcNow() + SessionLifetime));

            return true;
        }
    }

    public bool IsValidSession(string? sessionToken)
    {
        lock (_lock)
        {
            RemoveExpired(_sessions);

            return IndexOfMatch(_sessions, sessionToken) >= 0;
        }
    }

    /// <summary>True while at least one session is still valid. A connection that was opened earlier uses this to find out that every session has expired.</summary>
    public bool HasValidSession()
    {
        lock (_lock)
        {
            RemoveExpired(_sessions);

            return _sessions.Count > 0;
        }
    }

    private static string NewSecret()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));
    }

    private static byte[] HashOf(string? secret)
    {
        // Anything too long to be one of our secrets is hashed as empty text, so it cannot match and cannot cost much.
        var text = secret is { Length: > 0 and <= MaxPresentedLength } ? secret : string.Empty;

        return SHA256.HashData(Encoding.UTF8.GetBytes(text));
    }

    private static int IndexOfMatch(List<Entry> entries, string? presented)
    {
        var presentedHash = HashOf(presented);
        var found = -1;
        for (var index = 0; index < entries.Count; index++)
        {
            if (CryptographicOperations.FixedTimeEquals(entries[index].Hash, presentedHash) && !string.IsNullOrEmpty(presented))
            {
                found = index;
            }
        }

        return found;
    }

    private void RemoveExpired(List<Entry> entries)
    {
        var now = _timeProvider.GetUtcNow();
        entries.RemoveAll(entry => entry.ExpiresAt <= now);
    }
}
