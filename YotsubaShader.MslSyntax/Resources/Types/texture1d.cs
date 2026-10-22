using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.MslSyntax.Resources.Types;

[Texture(TextureDimension.D1, TextureComponentTypes.GenericDefine)]
public struct texture1d<T> where T : unmanaged;