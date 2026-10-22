using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.MslSyntax.Resources.Types;

[Texture(TextureDimension.D2, TextureComponentTypes.GenericDefine, Arrayed = true)]
public struct texture2d_array<T> where T : unmanaged;