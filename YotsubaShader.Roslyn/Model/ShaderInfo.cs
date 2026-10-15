using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using YotsubaShader.Language;

namespace YotsubaShader.Roslyn.Model;

/// <summary>Un parámetro de un método de etapa, tal como lo declara el usuario.</summary>
public sealed record ParameterInfo(IParameterSymbol Symbol)
{
    public string Name => Symbol.Name;
    public ITypeSymbol Type => Symbol.Type;
    public RefKind RefKind =>Symbol.RefKind;
}

/// <summary>Un método marcado con <c>[ShaderStage]</c>.</summary>
public sealed record EntryPointInfo(
    IMethodSymbol Method,
    Stage Stage,
    ImmutableArray<ParameterInfo> Parameters,
    ITypeSymbol ReturnType)
{
    public string Name => Method.Name;
}

/// <summary>La "ficha" de un shader: la clase y las etapas que declara.</summary>
public sealed record ShaderInfo(
    INamedTypeSymbol Type,
    ImmutableArray<EntryPointInfo> EntryPoints)
{
    public string Name => Type.Name;

    public string FullName => Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
}
