using YotsubaShader.Language.Native.Types;
using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.GlslSyntax.Resources.Types;

[Sampler, Texture(TextureDimension.Cube, TextureComponentTypes.Float)]
public struct samplerCube;