using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.HlslSyntax.Resources.Types;

[Texture(TextureDimension.D1, TextureComponentTypes.Float)]
public struct Texture1D;

[Texture(TextureDimension.D1, TextureComponentTypes.GenericDefine)]
public struct Texture1D<T> where T : unmanaged;