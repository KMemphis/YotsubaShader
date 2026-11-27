using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using YotsubaShader.Hub.Setup;

namespace YotsubaShader.Hub.Updates;

internal sealed record ReleaseAsset(string Name, string Url, long Size);

/// <param name="Assets">Null in a state file written before assets were kept; the next check refills it.</param>
internal sealed record ReleaseInfo(string Tag, string Version, string Name, string Notes, DateTimeOffset PublishedAt, string Url, bool Prerelease, IReadOnlyList<ReleaseAsset>? Assets = null);

internal sealed record UpdateStatus(
    string HubVersion, string? LatestVersion, bool HubUpdateAvailable,
    string? CompilerVersion, bool CompilerUpdateAvailable,
    bool CanSelfUpdate, DateTimeOffset? CheckedAt, string? Error, IReadOnlyList<ReleaseInfo> Releases);

/// <summary>
/// Polls the GitHub releases feed of the project (with ETag, so an unchanged feed costs nothing against the rate limit),
/// keeps the last answer on disk, and raises <see cref="NewRelease"/> once per version.
/// </summary>
internal sealed class UpdateService
{
    public const string Repo = "KMemphis/YotsubaShader";
    private const int Keep = 10;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly HttpClient _http;
    internal HttpClient Http => _http;
    private readonly string _feed;
    private readonly string _stateFile;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private State _state;
    private string? _error;
    private (DateTimeOffset At, string? Version)? _compiler;

    public UpdateService()
    {
        // Overridable so a test can point the Hub at a local feed; the default is the project's public feed.
        _feed = Environment.GetEnvironmentVariable("YOTSUBA_HUB_RELEASES_URL") is { Length: > 0 } f
            ? f : $"https://api.github.com/repos/{Repo}/releases?per_page={Keep}";
        _stateFile = Path.Combine(HubData.Dir, "updates.json");

        HubVersion = (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
            .Split('+')[0];
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"YotsubaShaderHub/{HubVersion}");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _state = Load();
    }

    public string HubVersion { get; }

    /// <summary>Raised on the first check that sees a stable release newer than this Hub that was not announced before.</summary>
    public event Action<ReleaseInfo>? NewRelease;

    public void StartBackground(CancellationToken ct) => _ = Task.Run(async () =>
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), ct);
            while (!ct.IsCancellationRequested)
            {
                await CheckAsync();
                await Task.Delay(TimeSpan.FromHours(6), ct);
            }
        }
        catch (OperationCanceledException) { }
    }, ct);

    public async Task CheckAsync()
    {
        await _gate.WaitAsync();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _feed);
            if (_state.Etag is { Length: > 0 } etag && _state.Releases.Count > 0 && _state.Releases.All(r => r.Assets is not null))
                req.Headers.TryAddWithoutValidation("If-None-Match", etag);

            using var res = await _http.SendAsync(req);
            if (res.StatusCode == HttpStatusCode.NotModified)
            {
                _error = null;
            }
            else if (res.IsSuccessStatusCode)
            {
                _state.Releases = Parse(await res.Content.ReadAsStringAsync());
                _state.Etag = res.Headers.ETag?.ToString();
                _error = null;
            }
            else
            {
                _error = res.StatusCode == HttpStatusCode.Forbidden
                    ? "GitHub rate limit reached. It will be retried later."
                    : $"The release feed answered {(int)res.StatusCode}.";
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            _error = e is JsonException ? "The release feed could not be read." : "Could not reach the release feed.";
        }
        finally
        {
            if (_error is null) _state.CheckedAt = DateTimeOffset.UtcNow;
            _gate.Release();
        }

        var latest = Latest();
        if (_error is null && latest is not null && IsNewer(latest.Version, HubVersion) && latest.Tag != _state.LastNotified)
        {
            _state.LastNotified = latest.Tag;
            NewRelease?.Invoke(latest);
        }
        Save();
    }

    public async Task<UpdateStatus> GetStatusAsync(bool canSelfUpdate)
    {
        var latest = Latest();
        var compiler = await CompilerVersionAsync();
        return new UpdateStatus(
            HubVersion, latest?.Version, latest is not null && IsNewer(latest.Version, HubVersion),
            compiler, latest is not null && compiler is not null && IsNewer(latest.Version, compiler),
            canSelfUpdate, _state.CheckedAt, _error, _state.Releases);
    }

    public ReleaseInfo? LatestRelease() => Latest();

    private ReleaseInfo? Latest() =>
        _state.Releases.Where(r => !r.Prerelease).OrderByDescending(r => Core(r.Version)).FirstOrDefault();

    private async Task<string?> CompilerVersionAsync()
    {
        if (_compiler is { } c && DateTimeOffset.UtcNow - c.At < TimeSpan.FromSeconds(5)) return c.Version; // short: the Install tab changes it
        var v = (await Probe.RunAsync()).CliVersion;
        _compiler = (DateTimeOffset.UtcNow, v);
        return v;
    }

    private static List<ReleaseInfo> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<ReleaseInfo>();
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.GetProperty("draft").GetBoolean()) continue;
            var tag = r.GetProperty("tag_name").GetString() ?? "";
            var url = r.GetProperty("html_url").GetString() ?? "";
            if (Core(tag) is null) continue;
            list.Add(new ReleaseInfo(
                tag, tag.TrimStart('v'), r.GetProperty("name").GetString() is { Length: > 0 } n ? n : tag,
                r.GetProperty("body").GetString() ?? "",
                r.GetProperty("published_at").GetDateTimeOffset(), url, r.GetProperty("prerelease").GetBoolean(),
                r.TryGetProperty("assets", out var assets)
                    ? assets.EnumerateArray().Select(a => new ReleaseAsset(
                        a.GetProperty("name").GetString() ?? "", a.GetProperty("browser_download_url").GetString() ?? "", a.GetProperty("size").GetInt64())).ToList()
                    : []));
        }
        return list.Take(Keep).ToList();
    }

    /// <summary>"v0.9.6-rc.1+abc" → 0.9.6. A pre-release of the same core compares as lower via <see cref="IsNewer"/>.</summary>
    private static Version? Core(string v)
    {
        var s = v.TrimStart('v', 'V').Split('-', '+')[0];
        return Version.TryParse(s, out var ver) ? ver : null;
    }

    private static bool IsNewer(string candidate, string current) =>
        Core(candidate) is { } a && Core(current) is { } b && a > b;

    private State Load()
    {
        try { return JsonSerializer.Deserialize<State>(File.ReadAllText(_stateFile), Json) ?? new State(); }
        catch { return new State(); }
    }

    private void Save()
    {
        try { File.WriteAllText(_stateFile, JsonSerializer.Serialize(_state, Json)); }
        catch (IOException) { /* a read-only profile must not break the app */ }
    }

    private sealed class State
    {
        public string? Etag { get; set; }
        public DateTimeOffset? CheckedAt { get; set; }
        public string? LastNotified { get; set; }
        [JsonInclude] public List<ReleaseInfo> Releases { get; set; } = [];
    }
}
