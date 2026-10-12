namespace YotsubaShader.Language.Attributes.MetalStyle;

[AttributeUsage(AttributeUsagesTable.Output)]
public class depth(string mode = "any") : Attribute
{
    public string Mode { get; } = mode;
}
