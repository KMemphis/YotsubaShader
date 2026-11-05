namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;

public record FunctionCallExpression(
    FunctionId Target, 
    IReadOnlyList<Expression> Arguments) : Expression;