using System.Diagnostics;
using System.Runtime.InteropServices;

namespace YotsubaShader.Hub.Setup;

internal sealed record Environ(string? DotnetPath, string[] Sdks, string? CliVersion, string DefaultHome, string Os, string CliPath);

internal static class Probe
{
    /// <summary>GUI apps often start with a minimal PATH, so look in the usual dotnet folders too.</summary>
    public static string? FindDotnet()
    {
        var exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dirs = new List<string>();
        if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root) dirs.Add(root);
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        dirs.AddRange(["/usr/local/share/dotnet", "/opt/homebrew/bin", "/usr/share/dotnet", "/usr/lib/dotnet",
            @"C:\Program Files\dotnet", Path.Combine(home, ".dotnet")]);
        return dirs.Where(d => d.Length > 0).Select(d => Path.Combine(d, exe)).FirstOrDefault(File.Exists);
    }

    public static string CliPath()
    {
        var exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yotsuba.exe" : "yotsuba";
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", exe);
    }

    public static string DefaultHome() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".yotsuba");

    public static async Task<Environ> RunAsync()
    {
        var dotnet = FindDotnet();
        string[] sdks = [];
        string? cli = null;
        if (dotnet is not null)
        {
            sdks = (await Capture(dotnet, "--list-sdks")).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Split(' ')[0].Trim()).ToArray();
            foreach (var line in (await Capture(dotnet, "tool", "list", "-g")).Split('\n'))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[0].Equals(Catalog.CliPackage, StringComparison.OrdinalIgnoreCase)) cli = parts[1];
            }
        }
        return new Environ(dotnet, sdks, cli, DefaultHome(), RuntimeInformation.OSDescription, CliPath());
    }

    private static async Task<string> Capture(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            var output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return output;
        }
        catch { return ""; }
    }
}
