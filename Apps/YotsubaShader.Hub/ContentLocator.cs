namespace YotsubaShader.Hub;

/// <summary>Finds the folders to serve: bundled next to the app in a release, or the repo checkout in development.</summary>
internal sealed record Content(string? Ui, string? Docs, string? Site, string? License);

internal static class ContentLocator
{
    public static Content Find()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "content");
        if (Directory.Exists(bundled))
            return new Content(Dir(bundled, "ui"), Dir(bundled, "learn"), Dir(bundled, "site"), File(bundled, "LICENSE"));

        var repo = FindRepoRoot();
        return repo is null
            ? new Content(null, null, null, null)
            : new Content(
                Dir(repo, "Apps", "YotsubaShader.Hub", "ui", "dist"),
                Dir(repo, "Web", "learn", "dist"),
                Dir(repo, "Web", "dist"),
                File(repo, "LICENSE"));
    }

    public static string Missing(string what, string how) =>
        $"<body style=\"font:16px -apple-system,system-ui;padding:48px\"><h2>{what} not built</h2><p>Run <code>{how}</code>.</p></body>";

    private static string? Dir(params string[] parts)
    {
        var path = Path.Combine(parts);
        return Directory.Exists(path) ? path : null;
    }

    private static string? File(params string[] parts)
    {
        var path = Path.Combine(parts);
        return System.IO.File.Exists(path) ? path : null;
    }

    private static string? FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (System.IO.File.Exists(Path.Combine(dir.FullName, "YotsubaShader.sln"))) return dir.FullName;
        return null;
    }
}
