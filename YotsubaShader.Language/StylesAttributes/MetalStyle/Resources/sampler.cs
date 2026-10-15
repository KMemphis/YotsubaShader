namespace YotsubaShader.Language.StylesAttributes.MetalStyle.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class sampler(int n) : YotsubaShaderAttribute
{
    public int N = n;
}
