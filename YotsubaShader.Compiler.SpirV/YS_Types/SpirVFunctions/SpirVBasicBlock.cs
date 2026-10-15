using System.Collections.Immutable;
using YotsubaShader.Compiler.SpirV.Builder;

namespace YotsubaShader.Compiler.SpirV.YS_Types.SpirVFunctions;

public readonly record struct SpirVBasicBlock(
    string LabelId,
    ImmutableArray<SpirVInstruction> Instructions
    );