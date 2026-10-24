namespace YotsubaShader.IR.Intermediate.FunctionBody;

public record VariableDeclaration(Storage Storage, Expression? Initializer) : Statement;