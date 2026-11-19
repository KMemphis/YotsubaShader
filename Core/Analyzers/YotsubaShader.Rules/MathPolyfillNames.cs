using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>
/// Sobrecargas de .NET que el compilador implementa como funciones propias (Polyfills/MathPolyfills.cs en
/// YotsubaShader.Roslyn). Debe coincidir exactamente con ese archivo: lo que no está aquí es un error YS0406.
/// </summary>
internal static class MathPolyfillNames
{
    private static readonly HashSet<string> Signatures = new()
    {
        "Math.Round(double,int)", "Math.Round(double,System.MidpointRounding)", "Math.Round(double,int,System.MidpointRounding)",
        "MathF.Round(float,int)", "MathF.Round(float,System.MidpointRounding)", "MathF.Round(float,int,System.MidpointRounding)",
        "Math.Cbrt(double)", "MathF.Cbrt(float)",
        "Math.CopySign(double,double)", "MathF.CopySign(float,float)",
        "Math.IEEERemainder(double,double)", "MathF.IEEERemainder(float,float)",
        "Math.MaxMagnitude(double,double)", "MathF.MaxMagnitude(float,float)",
        "Math.MinMagnitude(double,double)", "MathF.MinMagnitude(float,float)",
        "Math.ILogB(double)", "MathF.ILogB(float)",
        "MathF.BitIncrement(float)", "MathF.BitDecrement(float)",
        "Math.DivRem(int,int,out int)", "Math.DivRem(uint,uint,out uint)",
        "Math.DivRem(int,int)", "Math.DivRem(uint,uint)",
    };

    public static bool Contains(IMethodSymbol method) => Signatures.Contains(Signature(method));

    private static string Signature(IMethodSymbol method) =>
        method.ContainingType.Name + "." + method.Name + "(" +
        string.Join(",", method.Parameters.Select(p => (p.RefKind == RefKind.Out ? "out " : "") + p.Type.ToDisplayString())) + ")";
}
