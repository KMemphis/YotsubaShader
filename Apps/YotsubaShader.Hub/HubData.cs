namespace YotsubaShader.Hub;

/// <summary>Where the Hub keeps its own small files (update state, forum session). Overridable so tests never touch a real profile.</summary>
internal static class HubData
{
    public static string Dir
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable("YOTSUBA_HUB_DATA") is { Length: > 0 } d
                ? d : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YotsubaShaderHub");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
