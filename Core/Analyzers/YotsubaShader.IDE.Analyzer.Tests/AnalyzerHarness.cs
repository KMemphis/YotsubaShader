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
        })
        .Distinct()
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToArray();

    public static ImmutableArray<Diagnostic> Analyze(string code, string? targets = null)
    {
        var tree = CSharpSyntaxTree.ParseText(Usings + code, new CSharpParseOptions(LanguageVersion.Latest), path: "Shader.cs");
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable, allowUnsafe: true));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("El shader de prueba no compila en C#:\n" + string.Join("\n", errors));
        }

        var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new TargetsOptionsProvider(targets));
        return compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ShaderAnalyzer()), options)
            .GetAnalyzerDiagnosticsAsync().Result
            .Where(d => d.Id.StartsWith("YS"))
            .ToImmutableArray();
    }

    /// <summary>Comprueba que aparece el diagnóstico <paramref name="id"/> y devuelve el primero.</summary>
    public static Diagnostic Expect(string code, string id, string? targets = null)
    {
        var diagnostics = Analyze(code, targets);
        Diagnostic? found = diagnostics.FirstOrDefault(d => d.Id == id);
        Assert.True(found is not null,
            $"Se esperaba {id}. Diagnósticos: {(diagnostics.IsEmpty ? "ninguno" : string.Join("; ", diagnostics.Select(d => d.Id + " " + d.GetMessage())))}");
        return found!;
    }

    public static void ExpectNone(string code, string id, string? targets = null)
    {
        var diagnostics = Analyze(code, targets);
        Assert.DoesNotContain(diagnostics, d => d.Id == id);
    }

    /// <summary>Ningún error: el shader debe compilar para la GPU.</summary>
    public static void ExpectNoErrors(string code)
    {
        var errors = Analyze(code).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, "Errores inesperados: " + string.Join("; ", errors.Select(d => d.Id + " " + d.GetMessage())));
    }

    private sealed class TargetsOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly Options _options;
        public TargetsOptionsProvider(string? targets) => _options = new Options(targets);
        public override AnalyzerConfigOptions GlobalOptions => _options;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

        private sealed class Options : AnalyzerConfigOptions
        {
            private readonly string? _targets;
            public Options(string? targets) => _targets = targets;

            public override bool TryGetValue(string key, out string value)
            {
                value = _targets ?? "";
                return key == "build_property.YotsubaShaderTargets" && _targets is not null;
            }
        }
    }
}
