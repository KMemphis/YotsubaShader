using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace YotsubaShader.Hub.Updates;

/// <summary>
/// Finds the Hub installer of this system and architecture in the newest release, downloads it and opens it. Releases carry six
/// of them, named Yotsuba-Shader-Hub-&lt;rid&gt;.&lt;ext&gt; (win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, linux-arm64).
/// </summary>
internal static partial class InstallerDownload
{
    private const long MaxBytes = 1L << 30;
    private static readonly string AssetPrefix = $"https://github.com/{UpdateService.Repo}/releases/download/";

    public static string Rid => AppUpdater.Channel;

    private static string[] Extensions =>
        OperatingSystem.IsWindows() ? [".exe"] : OperatingSystem.IsMacOS() ? [".pkg", ".dmg"] : [".appimage"];

    /// <summary>Spaces, dots and underscores all count as the same separator, and case does not matter (GitHub rewrites spaces to dots).</summary>
    private static string Normalize(string name) => Separators().Replace(name.ToLowerInvariant(), "-");

    [GeneratedRegex("[ ._]+")] private static partial Regex Separators();

    public static ReleaseAsset? Pick(IEnumerable<ReleaseAsset> assets, string rid, string[] extensions) =>
        assets.FirstOrDefault(a =>
        {
            var ext = Path.GetExtension(a.Name).ToLowerInvariant();
            var stem = Normalize(Path.GetFileNameWithoutExtension(a.Name));
            return extensions.Contains(ext) && stem.StartsWith("yotsuba-shader-hub", StringComparison.Ordinal)
                && stem.EndsWith("-" + rid, StringComparison.Ordinal)
                && a.Url.StartsWith(AssetPrefix, StringComparison.Ordinal);
        });

    /// <summary>Returns the saved file, or an error message.</summary>
    public static async Task<(string? Path, string? Error)> RunAsync(ReleaseInfo? release, HttpClient http, CancellationToken ct)
    {
        if (release?.Assets is null) return (null, "The release list has no file information yet. Press Check now and try again.");
        var asset = Pick(release.Assets, Rid, Extensions);
        if (asset is null)
            return (null, $"Release {release.Version} has no installer for this computer. Expected a file named Yotsuba-Shader-Hub-{Rid}{Extensions[0]}.");
        if (asset.Size is <= 0 or > MaxBytes) return (null, "The installer has an unexpected size, so it was not downloaded.");

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, Path.GetFileName(asset.Name));
        var temp = target + ".part";
        try
        {
            using var res = await http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode) return (null, $"GitHub answered {(int)res.StatusCode} for the installer.");
            await using (var file = File.Create(temp))
                await (await res.Content.ReadAsStreamAsync(ct)).CopyToAsync(file, ct);
            if (new FileInfo(temp).Length != asset.Size) { File.Delete(temp); return (null, "The download is incomplete. Try again."); }
            File.Move(temp, target, overwrite: true);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException)
        {
            if (File.Exists(temp)) File.Delete(temp);
            return (null, $"The installer could not be downloaded: {e.Message}");
        }

        try
        {
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS()) File.SetUnixFileMode(target, (UnixFileMode)0b111_101_101);
            // Open it for the user: the installer itself, or its folder on Linux where an AppImage is run by hand.
            Process.Start(new ProcessStartInfo(OperatingSystem.IsLinux() ? folder : target) { UseShellExecute = true });
        }
        catch (Exception) { /* saved anyway; the UI shows where */ }
        return (target, null);
    }
}
