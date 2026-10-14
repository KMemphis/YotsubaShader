using YotsubaShader.IR.Intermediate.Slot;

namespace YotsubaShader.IR.Intermediate.Types;

public record Texture(string HumanName, StorageKind Kind, InterfaceSlot? Slot) : Storage(HumanName, Kind, Slot)
{
    
    public required TextureDimensions Dimensions;
    /// <summary>
    /// Tipo de componentes
    /// </summary>
    public required TextureComponentTypes TextureComponentsType;

    public required TextureDepth Depth;

    public required bool Arrayed;

    public required bool Multisampled;

    public required TextureUsage Usage;

    public required TextureFormat Format;
}

public enum TextureDimensions
{
    Texture_1D,
    Texture_2D,
    Texture_3D,
    Cube
}

public enum TextureComponentTypes
{
    Float,
    Int,
    Uint
}

public enum TextureUsage
{
    Unknown = 0,
    Sampled = 1,
    Storage = 2
}

public enum TextureFormat
{
    Unknown = 0,
    Rgba32f = 1,
    Rgba16f = 2,
    R32f = 3,
    Rgba8 = 4,
    Rgba8Snorm = 5,
    Rgba32i = 21,
    R32i = 24,
    Rgba32ui = 30,
    R32ui = 33
}

public enum TextureDepth
{
    No = 0,
    Yes = 1,
    Unknown = 2
}