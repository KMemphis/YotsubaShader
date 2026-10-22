using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.HlslSyntax.Resources.Types;

[Texture(TextureDimension.D2, TextureComponentTypes.Float, Arrayed = true)]
public struct Texture2DArray;

[Texture(TextureDimension.D2, TextureComponentTypes.GenericDefine, Arrayed = true)]
public struct Texture2DArray<T> where T : unmanaged;