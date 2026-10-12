namespace YotsubaShader.Language.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class LocationAttribute(int location) : Attribute
{
    public int Location = location;
}