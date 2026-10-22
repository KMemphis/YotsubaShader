using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.MslSyntax.Resources.Types;

[Texture(TextureDimension.D2, TextureComponentTypes.GenericDefine, Depth = TextureDepth.Yes)]
public class depth2d<T> where T : unmanaged;