namespace YotsubaShader.Language.Attributes.WgslStyle;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class @binding(int n) : Attribute
{
    public int N = n;
}
