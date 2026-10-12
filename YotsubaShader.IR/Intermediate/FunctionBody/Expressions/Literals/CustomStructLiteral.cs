using YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;

public record CustomStructLiteral(CustomStruct StructType, IReadOnlyList<Literal> Properties) : Literal;