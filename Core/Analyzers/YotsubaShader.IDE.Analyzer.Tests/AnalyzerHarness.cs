using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using YotsubaShader.IDE.Analyzer;

namespace YotsubaShader.IDE.Analyzer.Tests;

/// <summary>Compila un shader de prueba y ejecuta el analizador de YotsubaShader sobre él.</summary>
internal static class AnalyzerHarness
{
    public const string Usings = """
        using YotsubaShader.Language;
        using YotsubaShader.HlslSyntax;
        using YotsubaShader.HlslSyntax.Types;
        using YotsubaShader.HlslSyntax.Methods;
        using YotsubaShader.HlslSyntax.Resources;
        using YotsubaShader.HlslSyntax.Resources.Types;
        using YotsubaShader.SpirvSyntax.Resources;
        using YotsubaShader.Compiler.Metadata;

        """;

    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Concat(new[]
        {
            typeof(YotsubaShader.Language.ShaderStageAttribute).Assembly.Location,
            typeof(YotsubaShader.HlslSyntax.SV_Position).Assembly.Location,
            typeof(YotsubaShader.SpirvSyntax.Resources.Binding).Assembly.Location,
            typeof(YotsubaShader.Concepts.YotsubaShaderConcept).Assembly.Location,
            typeof(YotsubaShader.WgslSyntax.Resources.storage).Assembly.Location,
            typeof(YotsubaShader.Compiler.Metadata.ShaderTargetsAttribute).Assembly.Location,
        })
        .Distinct()
        // El analizador lleva copias internas de Target y GameEngine: la compilación del shader usa las de Metadata.
        .Where(path => Path.GetFileName(path) != "YotsubaShader.IDE.Analyzer.dll")
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToArray();

    /// <summary>
    /// <paramref name="targets"/> es el valor de <c>[assembly: ShaderTargets(...)]</c> (p. ej. <c>"Target.WebGpu"</c>) y
    /// <paramref name="engines"/> el de <c>[assembly: ShaderEngines(...)]</c> (<c>"GameEngine.Godot"</c>). Sin ellos
    /// el shader no declara nada y se valida contra Vulkan 1.3.
    /// </summary>
    public static ImmutableArray<Diagnostic> Analyze(string code, string? targets = null, string? engines = null)
    {
        string header = (targets is null ? "" : $"[assembly: ShaderTargets({targets})]\n") +
                        (engines is null ? "" : $"[assembly: ShaderEngines({engines})]\n");
        var tree = CSharpSyntaxTree.ParseText(Usings + header + code, new CSharpParseOptions(LanguageVersion.Latest), path: "Shader.cs");
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable, allowUnsafe: true));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("El shader de prueba no compila en C#:\n" + string.Join("\n", errors));
        }

        var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
        return compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ShaderAnalyzer()), options)
            .GetAnalyzerDiagnosticsAsync().Result
            .Where(d => d.Id.StartsWith("YS"))
            .ToImmutableArray();
    }

    /// <summary>Comprueba que aparece el diagnóstico <paramref name="id"/> y devuelve el primero.</summary>
    public static Diagnostic Expect(string code, string id, string? targets = null, string? engines = null)
    {
        var diagnostics = Analyze(code, targets, engines);
        Diagnostic? found = diagnostics.FirstOrDefault(d => d.Id == id);
        Assert.True(found is not null,
            $"Se esperaba {id}. Diagnósticos: {(diagnostics.IsEmpty ? "ninguno" : string.Join("; ", diagnostics.Select(d => d.Id + " " + d.GetMessage())))}");
        return found!;
    }

    public static void ExpectNone(string code, string id, string? targets = null, string? engines = null)
    {
        var diagnostics = Analyze(code, targets, engines);
        Assert.DoesNotContain(diagnostics, d => d.Id == id);
    }

    /// <summary>Ningún error: el shader debe compilar para la GPU.</summary>
    public static void ExpectNoErrors(string code)
    {
        var errors = Analyze(code).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, "Errores inesperados: " + string.Join("; ", errors.Select(d => d.Id + " " + d.GetMessage())));
    }
}
