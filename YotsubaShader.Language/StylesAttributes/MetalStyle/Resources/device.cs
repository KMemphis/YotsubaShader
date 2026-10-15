namespace YotsubaShader.Language.StylesAttributes.MetalStyle.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class device(bool readOnly = false) : YotsubaShaderAttribute
{
    public bool ReadOnly = readOnly;
}
