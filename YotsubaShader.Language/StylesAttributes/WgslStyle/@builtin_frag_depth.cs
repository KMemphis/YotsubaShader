namespace YotsubaShader.Language.Attributes.WgslStyle;

[AttributeUsage(AttributeUsagesTable.Output)]
public class @builtin_frag_depth(string mode = "") : Attribute
{
    public string Mode { get; } = mode;
}
