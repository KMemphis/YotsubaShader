using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using YotsubaShader.Hub.Setup;

namespace YotsubaShader.Hub.Shaders;

internal sealed record CompileResult(bool Ok, string? Wgsl, string Log);

/// <summary>
/// Compiles one C# shader to WGSL with the user's installed `yotsuba` CLI, so the gallery runs the same compiler
/// the user builds with. Each compile writes Shader.cs into a small project kept under the Hub's data folder
/// (the NuGet packages are restored once and reused afterwards).
/// </summary>
internal sealed partial class ShaderCompiler
{
    private static readonly Dictionary<string, string> Packages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hlsl"] = "YotsubaShader.HlslSyntax", ["Glsl"] = "YotsubaShader.GlslSyntax", ["Msl"] = "YotsubaShader.MslSyntax",
        ["Spirv"] = "YotsubaShader.SpirvSyntax", ["Wgsl"] = "YotsubaShader.WgslSyntax",
    };

    private const int MaxSource = 200_000;
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly string _root = Path.Combine(HubData.Dir, "shader-lab");
    private string? _version;

    public async Task<CompileResult> CompileAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return new(false, null, "The shader is empty.");
        if (code.Length > MaxSource) return new(false, null, "The shader is too large.");

        var cli = Probe.CliPath();
        if (!File.Exists(cli)) return new(false, null, "The yotsuba CLI is not installed. Open the Install tab to set it up first.");

        await _one.WaitAsync(ct);
        try
        {
            _version ??= (await Probe.RunAsync()).CliVersion;
            if (_version is null) return new(false, null, "The yotsuba CLI is not installed. Open the Install tab to set it up first.");

            // One folder per set of syntax packages: the NuGet restore of a folder is only valid for its own references.
            var styles = UsedStyles(code);
            var dir = Path.Combine(_root, string.Join("-", styles).ToLowerInvariant() + "-" + _version);
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "Shader.cs"), code, ct);
            await File.WriteAllTextAsync(Path.Combine(dir, "Shader.csproj"), Project(styles, _version), ct);
            var output = Path.Combine(dir, "out");
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);

            var (exit, log) = await RunAsync(cli, dir, ["compile", "Shader.csproj", "--target", "wgsl", "--output", "out"], ct);
            var files = Directory.Exists(output) ? Directory.GetFiles(output, "*.wgsl", SearchOption.AllDirectories) : [];
            if (exit == 0 && files.Length > 0)
                return new(true, string.Join("\n", await Task.WhenAll(files.Order().Select(f => File.ReadAllTextAsync(f, ct)))), "");
            return new(false, null, Condense(log, dir));
        }
        finally { _one.Release(); }
    }

    private static List<string> UsedStyles(string code)
    {
        var used = UsingSyntax().Matches(code).Select(m => m.Groups[1].Value).Where(Packages.ContainsKey).Distinct().Order().ToList();
        if (used.Count == 0) used.Add("Hlsl");
        return used;
    }

    private static string Project(List<string> used, string version)
    {
        var refs = new StringBuilder($"    <PackageReference Include=\"YotsubaShader.Language\" Version=\"{version}\" />\n");
        foreach (var u in used) refs.Append($"    <PackageReference Include=\"{Packages[u]}\" Version=\"{version}\" />\n");
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
                <ImplicitUsings>disable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
            {refs}  </ItemGroup>
            </Project>
            """;
    }

    private static async Task<(int Exit, string Log)> RunAsync(string cli, string dir, string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(cli) { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        psi.Environment["DOTNET_NOLOGO"] = "1";
        // The CLI runs `dotnet restore`, and a GUI launch may not have dotnet on PATH.
        var dotnetDir = Probe.FindDotnet() is { } d ? Path.GetDirectoryName(d) : null;
        psi.Environment["PATH"] = string.Join(Path.PathSeparator, new[] { Path.GetDirectoryName(cli), dotnetDir, Environment.GetEnvironmentVariable("PATH") }.Where(s => !string.IsNullOrEmpty(s)));

        using var p = Process.Start(psi)!;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(Timeout);
        try
        {
            var read = Task.WhenAll(p.StandardOutput.ReadToEndAsync(limit.Token), p.StandardError.ReadToEndAsync(limit.Token));
            await p.WaitForExitAsync(limit.Token);
            var text = await read;
            return (p.ExitCode, text[0] + "\n" + text[1]);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
            if (ct.IsCancellationRequested) throw;
            return (-1, "The compiler took too long. Try again.");
        }
    }

    /// <summary>Keeps what the person can act on: the compiler's errors, without paths from the Hub's own workspace.</summary>
    private static string Condense(string log, string dir)
    {
        var lines = log.Replace("\r", "").Split('\n').Select(l => l.Replace(dir + Path.DirectorySeparatorChar, "").Trim()).Where(l => l.Length > 0).ToList();
        var errors = lines.Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase) || l.Contains(".wgsl:")).Distinct().Take(12).ToList();
        var picked = errors.Count > 0 ? errors : lines.TakeLast(8).ToList();
        var text = string.Join("\n", picked);
        return text.Length > 4000 ? text[..4000] : text;
    }

    [GeneratedRegex(@"using\s+(?:static\s+)?YotsubaShader\.(Hlsl|Glsl|Msl|Spirv|Wgsl)Syntax\b")]
    private static partial Regex UsingSyntax();
}
