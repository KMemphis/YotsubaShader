using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;

namespace YotsubaShader.IR.Intermediate.FunctionBody.Statements;

/// <summary>Llamada a una intrínseca como sentencia: descarta el resultado, o no lo tiene (Discard, barreras, ImageStore...).</summary>
public sealed record IntrinsicCallStatement(
    GPUIntrinsic Intrinsic,
    IReadOnlyList<Expression> Arguments,
    IReadOnlyList<Storage?> ArgumentTypes) : Statement;
