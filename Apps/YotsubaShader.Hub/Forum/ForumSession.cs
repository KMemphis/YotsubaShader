using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using YotsubaShader.Community;

namespace YotsubaShader.Hub.Forum;

internal sealed record SessionInfo(string Mode, bool SignedIn, Author? User, bool SigningIn, string? SignInUrl, string? SignInCode);

/// <summary>
/// Which forum the Hub shows and who the user is in it. Without a community server configured (YOTSUBA_COMMUNITY_URL)
/// it is the offline preview. With one, posting needs a Discord sign-in done in the system browser.
/// </summary>
internal sealed class ForumSession
{
    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(3);

    private readonly Uri? _backend;
    private readonly RemoteForumSource? _remote;
    private readonly string _tokenFile = Path.Combine(HubData.Dir, "community-session.json");
    private string? _token;
    private Author? _user;
    private volatile bool _signingIn;
    private string? _signInUrl, _signInCode;

    private ForumSession(Uri? backend)
    {
        _backend = backend;
        Source = backend is null ? new InMemoryForum() : _remote = new RemoteForumSource(backend, () => _token);
        if (backend is not null) _token = LoadToken();
    }

    public static ForumSession Create()
    {
        var raw = Environment.GetEnvironmentVariable("YOTSUBA_COMMUNITY_URL");
        if (string.IsNullOrWhiteSpace(raw)) return new ForumSession(null);
        // The token and the login id travel to this server: it must be https, or plain http on this machine for development.
        if (!Uri.TryCreate(raw.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || !(uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            // A GUI app that dies at start tells the user nothing: show the sample forum and say why in it.
            return new ForumSession(null) { ConfigProblem = "YOTSUBA_COMMUNITY_URL must be an https URL (or http://localhost for development), so the sample forum is shown." };
        return new ForumSession(uri);
    }

    /// <summary>Set when the community server is configured wrongly; shown in place of the usual notice.</summary>
    public string? ConfigProblem { get; private init; }

    public IForumSource Source { get; }
    public bool Remote => _backend is not null;

    /// <summary>The identity writes are made as; null when sign-in is needed first.</summary>
    public async Task<Author?> UserAsync(CancellationToken ct = default)
    {
        if (!Remote) return InMemoryForum.Me;
        if (_token is null) return null;
        if (_user is not null) return _user;
        try { return _user = await _remote!.MeAsync(ct); }
        catch (ForumException e) when (e.Status == 401) { SignOut(); return null; }
    }

    public async Task<SessionInfo> GetAsync(CancellationToken ct = default)
    {
        var user = await UserAsync(ct);
        return new SessionInfo(Remote ? "remote" : "local", user is not null, user, _signingIn, _signingIn ? _signInUrl : null, _signingIn ? _signInCode : null);
    }

    /// <summary>Opens the sign-in page in the system browser and waits (in the background) for the backend to hand the session over.</summary>
    public void BeginSignIn()
    {
        if (!Remote) return;
        if (_signingIn) throw new ForumException("A sign-in is already in progress.", 409);

        // An unguessable id ties this app to the browser tab; the backend gives the session to whoever presents it once.
        var login = Base64Url(RandomNumberGenerator.GetBytes(32));
        _signingIn = true;
        _signInUrl = new Uri(_backend!, $"auth/start?login={login}").ToString();
        _signInCode = LoginCode.From(login); // the sign-in page shows the same code; the user checks they match
        try { Process.Start(new ProcessStartInfo(_signInUrl) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { /* the UI offers the link too */ }
        _ = Task.Run(() => PollAsync(login));
    }

    public void SignOut()
    {
        _token = null; _user = null;
        try { File.Delete(_tokenFile); } catch (IOException) { }
    }

    private async Task PollAsync(string login)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = _backend, Timeout = TimeSpan.FromSeconds(10) };
            for (var until = DateTime.UtcNow + LoginWindow; DateTime.UtcNow < until; await Task.Delay(2000))
            {
                try
                {
                    using var res = await http.GetAsync($"auth/poll?login={login}");
                    if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return; // expired or already taken
                    if (!res.IsSuccessStatusCode) continue; // 202 = the user has not finished yet
                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                    if (doc.RootElement.TryGetProperty("token", out var t) && t.GetString() is { Length: > 0 } token)
                    {
                        _token = token; _user = null;
                        SaveToken(token);
                        return;
                    }
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException) { /* keep trying until the window closes */ }
            }
        }
        finally { _signingIn = false; }
    }

    private string? LoadToken()
    {
        try { return File.Exists(_tokenFile) ? JsonSerializer.Deserialize<Stored>(File.ReadAllText(_tokenFile))?.Token : null; }
        catch { return null; }
    }

    private void SaveToken(string token)
    {
        try
        {
            File.WriteAllText(_tokenFile, JsonSerializer.Serialize(new Stored(token)));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(_tokenFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (IOException) { /* the session just will not survive a restart */ }
    }

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record Stored(string Token);
}
