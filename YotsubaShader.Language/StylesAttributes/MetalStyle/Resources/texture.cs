namespace YotsubaShader.Language.StylesAttributes.MetalStyle.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class texture(int n): YotsubaShaderAttribute
{
    public int N = n;
}
