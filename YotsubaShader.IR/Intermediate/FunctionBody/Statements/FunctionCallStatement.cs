namespace YotsubaShader.IR.Intermediate.FunctionBody.Statements;

public sealed record class FunctionCallStatement(
    FunctionId Target, 
    IReadOnlyList<Expression> Arguments): Statement;