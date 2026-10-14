namespace YotsubaShader.Language.Attributes.VulkanStyle;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class DescriptorSet(int s) : Attribute
{
    public int S = s;
}
