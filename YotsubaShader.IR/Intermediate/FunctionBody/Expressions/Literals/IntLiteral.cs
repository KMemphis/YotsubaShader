namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;

public record IntLiteral(string Value, bool Signed, uint Bits = 32) : Literal;