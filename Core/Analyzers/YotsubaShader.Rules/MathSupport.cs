using Microsoft.CodeAnalysis;
using YotsubaShader.Concepts.Methods;

namespace YotsubaShader.Rules;

/// <summary>Qué métodos de System.Math/MathF tienen traducción a la GPU. La tabla es la del compilador.</summary>
internal static class MathSupport
{
    public static bool IsMath(IMethodSymbol method) =>
        method.ContainingType?.ToDisplayString() is "System.Math" or "System.MathF" or "System.BitConverter";

    public static bool IsSupported(IMethodSymbol method)
    {
        string key = method.ContainingType.ToDisplayString() + "." + method.Name;
        if (!CSharpIntrinsicToIntrinsicGPU.Map.TryGetValue(key, out Intrinsic intrinsic))
        {
            return SupportedFeatures.MathPolyfills && MathPolyfillNames.Contains(method);
        }

        // Math.Round(x, dígitos) o con MidpointRounding no es la intrínseca Round.
        if (intrinsic == Intrinsic.Round && method.Parameters.Length != 1)
        {
            return SupportedFeatures.MathPolyfills && MathPolyfillNames.Contains(method);
        }

        return true;
    }

    /// <summary>IsNaN/IsInfinity/IsFinite: su resultado depende de las optimizaciones de coma flotante.</summary>
    public static string? SpecialValueTest(IMethodSymbol method) =>
        method.Name is "IsNaN" or "IsInfinity" or "IsFinite" ? method.Name : null;
}
