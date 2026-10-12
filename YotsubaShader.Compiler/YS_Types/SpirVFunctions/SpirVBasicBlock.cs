using System.Collections.Immutable;
using YotsubaShader.Compiler.Builder;

namespace YotsubaShader.Compiler.YS_Types.SpirVFunctions;

public readonly record struct SpirVBasicBlock(
    string LabelId,
    ImmutableArray<SpirVInstruction> Instructions
    );