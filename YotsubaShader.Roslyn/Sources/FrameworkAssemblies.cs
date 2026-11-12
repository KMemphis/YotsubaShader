namespace YotsubaShader.Roslyn.Sources;

internal static class FrameworkAssemblies
{
    private static readonly string[] FrameworkFolders =
    [
        "Microsoft.NETCore.App",
        "Microsoft.AspNetCore.App",
        "Microsoft.WindowsDesktop.App",
        "NETStandard.Library",
    ];

    public static bool IsFramework(string path)
    {
        string[] segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return segments.Any(segment =>
            FrameworkFolders.Any(folder =>
                segment.StartsWith(folder, StringComparison.OrdinalIgnoreCase)));
    }
}