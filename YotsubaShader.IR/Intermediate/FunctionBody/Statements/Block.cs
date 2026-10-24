namespace YotsubaShader.IR.Intermediate.FunctionBody;

public record Block(IReadOnlyList<Statement> Statements) : Statement;