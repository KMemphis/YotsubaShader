namespace YotsubaShader.Language.StylesAttributes.MetalStyle;

[AttributeUsage(AttributeUsagesTable.Output)]
public class depth(string mode = "any") : YotsubaShaderAttribute
{
    public string Mode { get; } = mode;
}
