using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.MslSyntax.Resources.Types;

[Texture(TextureDimension.D2, TextureComponentTypes.GenericDefine)]
public struct texture2d<T> where T : unmanaged;