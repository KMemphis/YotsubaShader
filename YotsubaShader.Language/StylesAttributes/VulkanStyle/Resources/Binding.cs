namespace YotsubaShader.Language.StylesAttributes.VulkanStyle.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class Binding(int n) : YotsubaShaderAttribute
{
    public int N = n;
}
