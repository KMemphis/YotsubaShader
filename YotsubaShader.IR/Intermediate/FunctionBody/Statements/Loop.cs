namespace YotsubaShader.IR.Intermediate.FunctionBody.Statements;

public record Loop(Expression? Condition, Statement Body, Block Continuing, Expression? BottomCondition = null) : Statement;
