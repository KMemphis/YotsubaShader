namespace YotsubaShader.IR.Intermediate.FunctionBody;

public record UnaryExpression(UnaryOperator Operator, Expression Operand) : Expression;
public enum UnaryOperator { Negate, LogicalNot }