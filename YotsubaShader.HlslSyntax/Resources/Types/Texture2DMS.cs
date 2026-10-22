using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.HlslSyntax.Resources.Types;

[Texture(TextureDimension.D2, TextureComponentTypes.GenericDefine, Multisampled = true)]
public struct Texture2DMS<T> where T : unmanaged;