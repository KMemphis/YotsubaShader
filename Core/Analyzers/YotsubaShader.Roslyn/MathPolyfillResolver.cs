using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace YotsubaShader.Roslyn;

/// <summary>
/// Métodos de System.Math/MathF sin instrucción de GPU (Cbrt, CopySign, Round con dígitos...). Su implementación
/// en C# (Polyfills/MathPolyfills.cs, recurso embebido) se añade a la compilación del shader y la llamada se
/// redirige allí: se compila como cualquier función auxiliar.
/// </summary>
internal static class MathPolyfillResolver
{
    private const string PolyfillTypeName = "YotsubaShader.Generated.MathPolyfills";

    private static readonly Lazy<string> Source = new(() =>
    {
        using Stream stream = typeof(MathPolyfillResolver).Assembly.GetManifestResourceStream("YotsubaShader.Polyfills.MathPolyfills.cs")
                              ?? throw new InvalidOperationException("The Math polyfill source is missing from YotsubaShader.Roslyn.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>La compilación con el código de los polyfills añadido (una sola vez).</summary>
    public static Compilation AddTo(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(PolyfillTypeName) is not null) return compilation;

        var options = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions ?? CSharpParseOptions.Default;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(Source.Value, options, path: "YotsubaShader.MathPolyfills.g.cs");
        return compilation.AddSyntaxTrees(tree);
    }

    /// <summary>El polyfill que implementa <paramref name="method"/>, si lo hay.</summary>
    public static IMethodSymbol? Resolve(IMethodSymbol method, Compilation compilation)
    {
        string? container = method.ContainingType?.ToDisplayString();
        if (container is not ("System.Math" or "System.MathF")) return null;

        INamedTypeSymbol? polyfills = compilation.GetTypeByMetadataName(PolyfillTypeName);
        if (polyfills is null) return null;

        string name = method.Name;
        var parameters = method.Parameters.Select(p => p.Type).ToList();
        bool hasMode = parameters.Count > 0 && parameters[^1].ToDisplayString() == "System.MidpointRounding";

        if (name == "Round")
        {
            name = (parameters.Count, hasMode) switch
            {
                (2, false) => "RoundDigits",
                (2, true) => "RoundWithMode",
                (3, true) => "RoundDigitsWithMode",
                _ => name
            };
        }

        foreach (IMethodSymbol candidate in polyfills.GetMembers(name).OfType<IMethodSymbol>())
        {
            if (candidate.DeclaredAccessibility != Accessibility.Public) continue;
            if (candidate.Parameters.Length != parameters.Count) continue;

            bool matches = true;
            for (int i = 0; i < parameters.Count; i++)
            {
                ITypeSymbol expected = parameters[i].TypeKind == TypeKind.Enum
                    ? compilation.GetSpecialType(SpecialType.System_Int32)
                    : parameters[i];
                if (!SymbolEqualityComparer.Default.Equals(candidate.Parameters[i].Type, expected) ||
                    candidate.Parameters[i].RefKind != method.Parameters[i].RefKind)
                {
                    matches = false;
                    break;
                }
            }

            if (matches) return candidate;
        }

        return null;
    }
}
