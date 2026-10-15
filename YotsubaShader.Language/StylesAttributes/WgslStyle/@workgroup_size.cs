namespace YotsubaShader.Language.StylesAttributes.WgslStyle;

[AttributeUsage(AttributeUsagesTable.Method)]
public class @workgroup_size(uint x, uint y = 1, uint z = 1) : YotsubaShaderAttribute
{
    public uint X { get; } = x;
    public uint Y { get; } = y;
    public uint Z { get; } = z;
}
