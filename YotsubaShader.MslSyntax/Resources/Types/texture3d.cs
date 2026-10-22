using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.MslSyntax.Resources.Types;

[Texture(TextureDimension.D3, TextureComponentTypes.GenericDefine)]
public struct texture3d<T> where T : unmanaged;