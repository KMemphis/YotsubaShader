namespace YotsubaShader.IR.Intermediate.FunctionBody.Statements;

public record Block(IReadOnlyList<Statement> Statements) : Statement;