namespace YotsubaShader.Language.Native.Types.Texture;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class TextureAttribute(TextureDimension dimension, TextureComponentTypes componentTypes) : YotsubaShaderAttribute
{
    public TextureDimension Dimension = dimension;

    public TextureComponentTypes ComponentTypes = componentTypes;
    
    public TextureUsage Usage = TextureUsage.Sampled;

    public TextureDepth Depth = TextureDepth.No;

    public TextureFormat Format = TextureFormat.Unknown;

    public bool Arrayed = false;

    public bool Multisampled = false;
}