namespace YotsubaShader.Language.Attributes.VulkanStyle;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class Binding(int n) : Attribute
{
    public int N = n;
}
