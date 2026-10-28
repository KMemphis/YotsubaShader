namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions;

public record BinaryExpression(BinaryOperator Operator, 
    Expression Left, Expression Right) : Expression;

public enum BinaryOperator
{ 
    Add, 
    Subtract,
    Multiply,
    Divide,
    Remainder
}