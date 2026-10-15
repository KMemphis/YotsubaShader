namespace YotsubaShader.Language.Native.Types.Vector;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class VectorComponentAttribute(int index) : YotsubaShaderAttribute
{
    public int Index { get; } = index;
}