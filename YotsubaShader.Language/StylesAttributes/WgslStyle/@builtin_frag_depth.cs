namespace YotsubaShader.Language.StylesAttributes.WgslStyle;

[AttributeUsage(AttributeUsagesTable.Output)]
public class @builtin_frag_depth(string mode = "") : YotsubaShaderAttribute
{
    public string Mode { get; } = mode;
}
