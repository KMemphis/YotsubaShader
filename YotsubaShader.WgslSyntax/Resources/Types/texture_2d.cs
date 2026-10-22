using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.WgslSyntax.Resources.Types;

[Texture(TextureDimension.D2, TextureComponentTypes.GenericDefine)]
public struct texture_2d<T> where T : unmanaged;