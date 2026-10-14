namespace YotsubaShader.Language.Attributes.MetalStyle;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class sampler(int n) : Attribute
{
    public int N = n;
}
