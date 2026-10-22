using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.SpirvSyntax.Resources.Type;

[Texture(TextureDimension.D1, TextureComponentTypes.GenericDefine)]
public struct Image1D<T> where T : unmanaged;