namespace YotsubaShader.Language.StylesAttributes.WgslStyle.Resources;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class storage(storage_use use) : YotsubaShaderAttribute
{
    public storage_use Use = use;
}

public enum storage_use
{
    read,
    read_write
}
