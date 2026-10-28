namespace YotsubaShader.IR.Intermediate.FunctionBody.Statements;

public record VariableDeclaration(Storage Storage, Expression? Initializer) : Statement;