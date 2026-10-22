using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.SpirvSyntax.Resources.Type;

[Texture(TextureDimension.Cube, TextureComponentTypes.GenericDefine)]
public struct ImageCube<T> where T : unmanaged;