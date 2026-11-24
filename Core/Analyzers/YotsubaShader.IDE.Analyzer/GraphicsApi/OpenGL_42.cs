using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 4.2 (GLSL 4.20): llegan las imágenes con load/store; compute solo con ARB_compute_shader.</summary>
internal static class OpenGL_42
{
    public static readonly ApiProfile Profile = OpenGL_41.Profile.Next(Target.OpenGL_42, "4.2")
        .Emits("glsl", "420")
        .Feature(ApiFeature.StorageTexture, Availability.Guaranteed)
        .Feature(ApiFeature.Compute, Availability.Optional, "needs ARB_compute_shader (offered by most 4.2 drivers, not macOS)");
}
