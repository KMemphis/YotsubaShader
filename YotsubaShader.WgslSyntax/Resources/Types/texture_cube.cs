using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.WgslSyntax.Resources.Types;

[Texture(TextureDimension.Cube, TextureComponentTypes.GenericDefine)]
public struct texture_cube<T> where T : unmanaged;