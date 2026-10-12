namespace YotsubaShader.IR.Intermediate;

public readonly record struct Shader(
    IReadOnlyList<Function> Functions,
    IReadOnlyList<Storage> Constants);