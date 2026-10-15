namespace YotsubaShader.Language.StylesAttributes.WgslStyle.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class @group(int s) : YotsubaShaderAttribute
{
    public int S = s;
}
