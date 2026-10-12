namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions;

public record Assignment(Expression Target, Expression Value) : Statement;