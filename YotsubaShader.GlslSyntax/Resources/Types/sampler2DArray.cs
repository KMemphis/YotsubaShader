using YotsubaShader.Language.Native.Types;
using YotsubaShader.Language.Native.Types.Texture;

namespace YotsubaShader.GlslSyntax.Resources.Types;

[Sampler, Texture(TextureDimension.D2, TextureComponentTypes.Float, Arrayed = true)]
public struct sampler2Array;