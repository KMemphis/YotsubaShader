namespace YotsubaShader.Language.StylesAttributes.GlslStyle.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class layout(int set, int binding) : YotsubaShaderAttribute
{
    public int Set = set;

    public int Binding = binding;
}
