using System.Collections.Concurrent;

namespace YotsubaShader.Community.Server.Auth;

/// <summary>
/// Hands a finished sign-in from the browser to the app that started it. The app polls with an unguessable login id;
/// the token is given out once. The id is only known to the app and to the browser it opened.
/// </summary>
public sealed class LoginStore
{
    private enum State { Started, Ready, Taken }
    private sealed record Entry(State State, string? Token, DateTime At);

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private const int MaxEntries = 2000;

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    /// <summary>A login id is 32 random bytes in base64url. Anything else is rejected before it is stored.</summary>
    public static bool IsValidId(string? id) =>
        id is { Length: 43 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public bool Start(string id)
    {
        Sweep();
        // Opening the sign-in page again (a refresh, a second tab) is fine while it is pending, but a login that finished
        // or was handed over can never be started again: that would let a finished session be reset or replayed.
        if (_entries.TryGetValue(id, out var existing)) return existing.State == State.Started;
        if (_entries.Count >= MaxEntries) return false;
        return _entries.TryAdd(id, new Entry(State.Started, null, DateTime.UtcNow));
    }

    public bool IsStarted(string id) => _entries.TryGetValue(id, out var e) && e.State == State.Started;

    public void Complete(string id, string token) => _entries[id] = new Entry(State.Ready, token, DateTime.UtcNow);

    /// <summary>Pending: not finished (or unknown). Expired: taken or timed out. Otherwise the token, once.</summary>
    public (bool Pending, string? Token) Take(string id)
    {
        if (!_entries.TryGetValue(id, out var e)) return (true, null);
        if (e.State == State.Taken || DateTime.UtcNow - e.At > Ttl) return (false, null);
        if (e.State == State.Started) return (true, null);
        _entries[id] = new Entry(State.Taken, null, DateTime.UtcNow);
        return (false, e.Token);
    }

    private void Sweep()
    {
        foreach (var (k, v) in _entries)
            if (DateTime.UtcNow - v.At > Ttl) _entries.TryRemove(k, out _);
    }
}
