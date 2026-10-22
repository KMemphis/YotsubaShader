using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.WgslSyntax.Resources.Types;

[Texture(TextureDimension.D2, TextureComponentTypes.GenericDefine, Arrayed = true)]
public struct texture_2d_array<T> where T : unmanaged;