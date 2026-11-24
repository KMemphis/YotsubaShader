using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 3.2 (GLSL 1.50): double solo con ARB_gpu_shader_fp64.</summary>
internal static class OpenGL_32
{
    public static readonly ApiProfile Profile = OpenGL_31.Profile.Next(Target.OpenGL_32, "3.2")
        .Emits("glsl", "150")
        .Feature(ApiFeature.Float64, Availability.Optional, "needs ARB_gpu_shader_fp64 (offered by most drivers, not macOS)");
}
