using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.MslSyntax.Resources.Types;

[Texture(TextureDimension.Cube, TextureComponentTypes.GenericDefine)]
public struct texturecube<T> where T : unmanaged;