using Microsoft.CodeAnalysis;

namespace YotsubaShader.Roslyn;

/// <summary>Un shader que no se pudo compilar: su nombre completo, el motivo y los diagnósticos YS si los hay.</summary>
public sealed record ShaderFailure(string Shader, string Message, IReadOnlyList<Diagnostic> Diagnostics);
