using YotsubaShader.IR.Intermediate.FunctionBody;

namespace YotsubaShader.IR.Intermediate.ValueConvertions;

public record NumericConversion(
    Expression Operand,
    ScalarType TargetType) : Expression;

public enum ScalarType
{
    Int32,
    UInt32,
    Float32,
    Float64
}