namespace YotsubaShader.Language.StylesAttributes.VulkanStyle.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class DescriptorSet(int s) : YotsubaShaderAttribute
{
    public int S = s;
}
