namespace YotsubaShader.Language.Attributes.MetalStyle;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class device(bool readOnly = false) : Attribute
{
    public bool ReadOnly = readOnly;
}
