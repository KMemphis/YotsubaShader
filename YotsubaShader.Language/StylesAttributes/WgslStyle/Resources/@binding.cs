namespace YotsubaShader.Language.StylesAttributes.WgslStyle.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class @binding(int n) : YotsubaShaderAttribute
{
    public int N = n;
}
