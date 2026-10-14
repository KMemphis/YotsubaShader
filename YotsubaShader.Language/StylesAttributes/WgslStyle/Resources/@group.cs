namespace YotsubaShader.Language.Attributes.WgslStyle;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class @group(int s) : Attribute
{
    public int S = s;
}
