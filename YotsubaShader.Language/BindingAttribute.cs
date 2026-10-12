namespace YotsubaShader.Language;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class BindingAttribute(int set, int binding) : Attribute
{
    public int Set = set;

    public int Binding = binding;
}