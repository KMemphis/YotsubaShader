namespace YotsubaShader.Language.Attributes.MetalStyle;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class texture(int n): Attribute
{
    public int N = n;
}
