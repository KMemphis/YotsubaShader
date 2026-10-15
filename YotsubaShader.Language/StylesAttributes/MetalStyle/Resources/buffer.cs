namespace YotsubaShader.Language.StylesAttributes.MetalStyle.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class buffer(int n) : YotsubaShaderAttribute
{
    public int N = n;
}
