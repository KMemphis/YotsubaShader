namespace YotsubaShader.Language.Native.Types.Matrix;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class MatrixColumnAttribute(int index) : YotsubaShaderAttribute
{
    public int Index { get; } = index;
}
