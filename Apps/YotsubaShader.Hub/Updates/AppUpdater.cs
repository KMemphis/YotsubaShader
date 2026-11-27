using System.Runtime.InteropServices;
using Velopack;
using Velopack.Sources;

namespace YotsubaShader.Hub.Updates;

/// <summary>
/// Self-update of the Hub through Velopack. It only works for an installed (Velopack-packaged) copy; a copy run from
/// source or unpacked from an archive reports <see cref="CanSelfUpdate"/> = false and the UI links to the release instead.
/// </summary>
internal sealed class AppUpdater
{
    private readonly UpdateManager? _manager;
    private int _busy;

    public AppUpdater()
    {
        try
        {
            _manager = new UpdateManager(
                new GithubSource($"https://github.com/{UpdateService.Repo}", null, false),
                new UpdateOptions { ExplicitChannel = Channel });
        }
        catch { _manager = null; }
    }

    /// <summary>
    /// One channel per platform and architecture (osx-arm64, win-x64...). Velopack's default is only "osx"/"win"/"linux",
    /// so two architectures of one release would overwrite each other's installer and feed. scripts/hub/pack.sh packs
    /// with the same names.
    /// </summary>
    public static string Channel =>
        $"{(OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux")}-" +
        (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64");

    public bool CanSelfUpdate => _manager?.IsInstalled == true;

    /// <summary>Downloads and applies the newest Velopack package, then restarts. Returns an error message, or null when the app is restarting.</summary>
    public async Task<string?> ApplyAsync()
    {
        if (_manager is null || !_manager.IsInstalled) return "This copy was not installed with the Hub installer, so it cannot update itself.";
        if (Interlocked.Exchange(ref _busy, 1) == 1) return "An update is already in progress.";
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null) return "No installable package was found for this release yet.";
            await _manager.DownloadUpdatesAsync(update);
            _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
            return null;
        }
        catch (Exception e)
        {
            return $"The update failed: {e.Message}";
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }
}
