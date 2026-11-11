namespace YotsubaShader.IR.Intermediate.FunctionBody.Statements;

public record If(Expression Condition, Statement Then, Statement? Else) : Statement;