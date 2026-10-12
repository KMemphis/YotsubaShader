using YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;

public record VectorLiteral(IReadOnlyList<string> Values, VectorComponentsType ComponentsType) : Literal;