using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.WgslSyntax.Resources.Types;

[Texture(TextureDimension.D3, TextureComponentTypes.GenericDefine)]
public struct texture_3d<T> where T : unmanaged;