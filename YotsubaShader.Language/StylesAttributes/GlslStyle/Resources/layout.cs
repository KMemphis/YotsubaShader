namespace YotsubaShader.Language.Attributes.GlslStyle;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class layout(int set, int binding) : Attribute
{
    public int Set = set;

    public int Binding = binding;
}
