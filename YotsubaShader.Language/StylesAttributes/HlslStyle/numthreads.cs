namespace YotsubaShader.Language.Attributes.HlslStyle;

[AttributeUsage(AttributeUsagesTable.Method)]
public class numthreads(uint x, uint y, uint z) : Attribute
{
    public uint X { get; } = x;
    public uint Y { get; } = y;
    public uint Z { get; } = z;
}
