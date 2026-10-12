using YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions.Literals;

public record MatrixLiteral(Matrix MatrixType, IReadOnlyList<IReadOnlyList<Literal>> Columns) : Literal;