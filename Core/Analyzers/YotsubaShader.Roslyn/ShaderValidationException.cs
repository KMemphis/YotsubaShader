using Microsoft.CodeAnalysis;

namespace YotsubaShader.Roslyn;

/// <summary>
/// El shader no cumple las reglas de YotsubaShader (los mismos diagnósticos YSxxxx que muestra el IDE).
/// </summary>
public sealed class ShaderValidationException : Exception
{
    public ShaderValidationException(IReadOnlyList<Diagnostic> diagnostics)
        : base(Format(diagnostics))
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    private static string Format(IReadOnlyList<Diagnostic> diagnostics) =>
        "The shader has errors:" + Environment.NewLine +
        string.Join(Environment.NewLine, diagnostics.Select(d => "  " + d));
}
