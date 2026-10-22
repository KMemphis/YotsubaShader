using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.SpirvSyntax.Resources.Type;

[Texture(TextureDimension.D3, TextureComponentTypes.GenericDefine)]
public struct Image3D<T> where T : unmanaged;