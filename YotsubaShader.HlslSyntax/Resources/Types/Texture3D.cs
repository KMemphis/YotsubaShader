using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.HlslSyntax.Resources.Types;

[Texture(TextureDimension.D3, TextureComponentTypes.Float)]
public struct Texture3D;

[Texture(TextureDimension.D3, TextureComponentTypes.GenericDefine)]
public struct Texture3D<T> where T : unmanaged;