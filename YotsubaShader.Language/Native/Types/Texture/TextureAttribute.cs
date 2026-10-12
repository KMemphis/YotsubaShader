namespace YotsubaShader.Language.Native.Types.Texture;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class TextureAttribute(TextureDimension dimension) : Attribute
{
    public TextureDimension Dimension = dimension;
}