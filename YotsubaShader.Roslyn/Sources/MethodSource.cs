using Microsoft.CodeAnalysis;

namespace YotsubaShader.Roslyn.Sources;

public sealed record MethodSource(IMethodSymbol Method, Compilation Compilation);

public interface IMethodSourceProvider
{
    MethodSource? TryGet(IMethodSymbol called);
}