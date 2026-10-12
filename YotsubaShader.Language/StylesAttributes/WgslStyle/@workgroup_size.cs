namespace YotsubaShader.Language.Attributes.WgslStyle;

[AttributeUsage(AttributeUsagesTable.Method)]
public class @workgroup_size(uint x, uint y = 1, uint z = 1) : Attribute
{
    public uint X { get; } = x;
    public uint Y { get; } = y;
    public uint Z { get; } = z;
}
