using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.HlslSyntax.Resources.Types;

[Texture(TextureDimension.Cube, TextureComponentTypes.Float, Arrayed = true)]
public class TextureCubeArray;

[Texture(TextureDimension.Cube, TextureComponentTypes.GenericDefine, Arrayed = true)]
public class TextureCubeArray<T> where T : unmanaged;