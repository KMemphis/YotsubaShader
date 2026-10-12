using Array = YotsubaShader.IR.Intermediate.Types.Array;

namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;

public record ArrayLiteral(Array ArrayType, IReadOnlyList<Literal> Elements) : Literal;