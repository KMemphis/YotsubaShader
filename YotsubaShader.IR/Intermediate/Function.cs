using YotsubaShader.IR.Intermediate.FunctionBody;

namespace YotsubaShader.IR.Intermediate;
public readonly record struct Function(
    string HumanName,
    Stage Stage,
    IReadOnlyList<Storage> Inputs,
    IReadOnlyList<Storage> Outputs,
    IReadOnlyList<Statement> Body);