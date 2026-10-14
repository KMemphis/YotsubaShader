namespace YotsubaShader.Language.Attributes.MetalStyle;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class buffer(int n) : Attribute
{
    public int N = n;
}
