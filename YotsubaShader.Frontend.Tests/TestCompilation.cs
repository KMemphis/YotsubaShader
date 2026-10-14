using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using YotsubaShader.Compiler.Stages;
using YotsubaShader.Language;

namespace YotsubaShader.Frontend.Tests;

/// <summary>Puerta 1 del libro: del texto a una compilación, y comprobar que compila.</summary>
internal static class TestCompilation
{
    private static readonly MetadataReference[] PlatformReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToArray();

    private static readonly MetadataReference YotsubaShaderReference =
        MetadataReference.CreateFromFile(typeof(ShaderStageAttribute).Assembly.Location);

    public static CSharpCompilation Create(string code, string name = "ShaderAnalysis",
        bool referenceYotsubaShader = true, params MetadataReference[] extra)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));

        IEnumerable<MetadataReference> references = PlatformReferences.Concat(extra);
        if (referenceYotsubaShader)
        {
            references = references.Append(YotsubaShaderReference);
        }

        CSharpCompilation compilation = CSharpCompilation.Create(name, [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Roslyn no se rompe con errores: devuelve símbolos de error. Primero errores de C#.
        Diagnostic[] errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException("El código de prueba no compila:\n" +
                                                string.Join('\n', errors.Select(e => e.ToString())));
        }

        return compilation;
    }
}
