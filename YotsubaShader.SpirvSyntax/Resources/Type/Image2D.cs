using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.SpirvSyntax.Resources.Type;

[Texture(TextureDimension.D2, TextureComponentTypes.GenericDefine)]
public struct Image2D<T> where T : unmanaged;