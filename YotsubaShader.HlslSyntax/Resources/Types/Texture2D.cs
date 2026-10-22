using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.HlslSyntax.Resources.Types;

[Texture(TextureDimension.D2, TextureComponentTypes.Float)]
public struct Texture2D;

[Texture(TextureDimension.D2, TextureComponentTypes.GenericDefine)]
public struct Texture2D<T> where T : unmanaged;