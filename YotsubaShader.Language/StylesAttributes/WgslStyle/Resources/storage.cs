namespace YotsubaShader.Language.Attributes.WgslStyle;

[AttributeUsage(AttributeUsagesTable.Resource)]
public class storage(storage_use use) : Attribute
{
    public storage_use Use = use;
}

public enum storage_use
{
    read,
    read_write
}
