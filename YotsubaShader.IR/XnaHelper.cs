using System.Globalization;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.IR;

public static class XnaHelper
{
    
    #region XNA Vectors

    public static Vector XNA_CreateVector2(string humanName, StorageKind kind, InterfaceSlot slot, float x, float y)
    {
        return new Vector(humanName, kind, slot)
        {
            ComponentsType = VectorComponentsType.Float,
            ComponentsLength = 2,
            ComponentsDefaultValues = new List<string>(2)
            {
                x.ToString(CultureInfo.InvariantCulture), 
                y.ToString(CultureInfo.InvariantCulture)
            },
        };
    }
    public static Vector XNA_CreateVector3(string humanName, StorageKind kind, InterfaceSlot slot, float x, float y, float z)
    {
        return new Vector(humanName, kind, slot)
        {
            ComponentsType = VectorComponentsType.Float,
            ComponentsLength = 3,
            ComponentsDefaultValues = new List<string>(3)
            {
                x.ToString(CultureInfo.InvariantCulture), 
                y.ToString(CultureInfo.InvariantCulture),
                z.ToString(CultureInfo.InvariantCulture)
            },
        };
    }
    #endregion

    #region XNA Textures

    public static Texture XNA_CreateTexture2D(string humanName, InterfaceSlot slot)
    {
        return new Texture(humanName, StorageKind.Texture, slot)
        {
            Dimensions = TextureDimensions.Texture_2D,
            TextureComponentsType = TextureComponentTypes.Float,
            Depth = false,
            Arrayed = false,
            Multisampled = false,
            Usage = TextureUsage.Sampled,
            Format = TextureFormat.Unknown
        };
    }

    #endregion

   
}