namespace YotsubaShader.IR.Intermediate.FunctionBody.Statements;

public record Return(Expression? Value) : Statement;