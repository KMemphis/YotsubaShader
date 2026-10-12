namespace YotsubaShader.IR.Intermediate.FunctionBody.Types;

public record Assign(Expression Target, Expression Value) : Statement;