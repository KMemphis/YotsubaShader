namespace YotsubaShader.Hub.Setup;

internal enum ComponentKind { Cli, Plugin, Bundled }

/// <summary>One checkbox of the installer tree. Plugin ids are the names the CLI knows (<c>yotsuba plugin install kni</c>).</summary>
internal sealed record Component(
    string Id, string Name, string Description, ComponentKind Kind,
    string? Group = null, bool Locked = false, bool Default = true, string[]? Requires = null);

internal static class Catalog
{
    public const string CliPackage = "YotsubaShader.Compiler.CLI";
    public const string CliId = "cli";

    public static readonly Component[] All =
    [
        new(CliId, "Compiler (yotsuba CLI)", "Compiles C# shaders to SPIR-V, HLSL, GLSL and MSL. Installed as the .NET tool `dotnet yotsuba`.",
            ComponentKind.Cli, Locked: true),
        new("monogame", "MonoGame", "Backend that produces MGFX effects (.mgfx).", ComponentKind.Plugin, "Engine plugins", Requires: [CliId]),
        new("kni", "KNI", "Backend that produces KNI WebGL effects (.knifx).", ComponentKind.Plugin, "Engine plugins", Requires: [CliId]),
        new("fna", "FNA", "Backend that produces FNA effects (.fxb).", ComponentKind.Plugin, "Engine plugins", Default: false, Requires: [CliId]),
        new("godot", "Godot", "Backend that produces Godot Shading Language (.gdshader).", ComponentKind.Plugin, "Engine plugins", Default: false, Requires: [CliId]),
        new("docs", "Offline documentation", "The Learn site, bundled with this release.", ComponentKind.Bundled, "Included with the Hub", Locked: true),
        new("playground", "Playground", "Write and compile shaders without leaving the app.", ComponentKind.Bundled, "Included with the Hub", Locked: true),
    ];

    /// <summary>Expands the selection with required components, in install order.</summary>
    public static IReadOnlyList<Component> Resolve(IEnumerable<string> ids)
    {
        var wanted = new HashSet<string>(ids);
        foreach (var c in All.Where(c => wanted.Contains(c.Id)).ToList())
            foreach (var r in c.Requires ?? []) wanted.Add(r);
        wanted.Add(CliId);
        return All.Where(c => wanted.Contains(c.Id) && c.Kind != ComponentKind.Bundled).ToList();
    }
}
