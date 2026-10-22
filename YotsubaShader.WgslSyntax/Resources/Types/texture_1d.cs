using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.WgslSyntax.Resources.Types;

[Texture(TextureDimension.D1, TextureComponentTypes.GenericDefine)]
public struct texture_1d<T> where T : unmanaged;