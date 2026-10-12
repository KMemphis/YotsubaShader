using System.Collections.Immutable;

namespace YotsubaShader.Compiler.YS_Types.SpirVFunctions;

public record struct SpirVFunction(
    string Id,
    string ReturnTypeId,
    string FunctionTypeId,
    ImmutableArray<SpirVFunctionParameter> FunctionParameters,
    ImmutableArray<SpirVBasicBlock> Blocks
    );