using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.HlslSyntax.Resources.Types;


[Texture(TextureDimension.Cube, TextureComponentTypes.Float)]
public struct TextureCube;

[Texture(TextureDimension.Cube, TextureComponentTypes.GenericDefine)]
public struct TextureCube<T> where T : unmanaged;