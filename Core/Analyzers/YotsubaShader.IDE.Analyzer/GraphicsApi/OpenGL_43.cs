using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 4.3 (GLSL 4.30): llegan compute shaders y storage buffers.</summary>
internal static class OpenGL_43
{
    public static readonly ApiProfile Profile = OpenGL_42.Profile.Next(Target.OpenGL_43, "4.3")
        .Emits("glsl", "430")
        .Feature(ApiFeature.Compute, Availability.Guaranteed)
        .Feature(ApiFeature.StorageBuffer, Availability.Guaranteed);
}
