namespace YotsubaShader.Language.StylesAttributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class LocationAttribute(int location) : YotsubaShaderAttribute
{
    public int Location = location;
}