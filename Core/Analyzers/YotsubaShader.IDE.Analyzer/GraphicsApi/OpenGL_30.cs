using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 3.0 (GLSL 1.30).</summary>
internal static class OpenGL_30
{
    public static readonly ApiProfile Profile = OpenGL_20.Profile.Next(Target.OpenGL_30, "3.0")
        .Emits("glsl", "130")
        .Feature(ApiFeature.UnsignedInt, Availability.Guaranteed)
        .Feature(ApiFeature.IntegerBitOps, Availability.Guaranteed)
        .Feature(ApiFeature.TexelFetch, Availability.Guaranteed)
        .Feature(ApiFeature.TextureOffset, Availability.Guaranteed)
        .Feature(ApiFeature.TextureQuerySize, Availability.Guaranteed)
        .Feature(ApiFeature.TextureGather, Availability.Optional, "needs ARB_texture_gather");
}
