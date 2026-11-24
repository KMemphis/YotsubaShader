using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 2.0 (GLSL 1.10).</summary>
internal static class OpenGL_20
{
    public static readonly ApiProfile Profile = ApiProfile.Create(Target.OpenGL_20, "OpenGL", "2.0", ShaderTargets.OpenGL)
        .Emits("glsl", "110")
        .Feature(ApiFeature.Float64, Availability.Unavailable, "does not exist before OpenGL 4.0")
        .Feature(ApiFeature.Float16, Availability.Optional, "needs an extension (EXT_shader_explicit_arithmetic_types)")
        .Feature(ApiFeature.Int16, Availability.Optional, "needs an extension (EXT_shader_explicit_arithmetic_types)")
        .Feature(ApiFeature.Int64, Availability.Optional, "needs ARB_gpu_shader_int64")
        .Feature(ApiFeature.Compute, Availability.Unavailable, "compute shaders arrive in OpenGL 4.3")
        .Feature(ApiFeature.StorageBuffer, Availability.Unavailable, "storage buffers arrive in OpenGL 4.3")
        .Feature(ApiFeature.StorageTexture, Availability.Unavailable, "images (load/store) arrive in OpenGL 4.2")
        .Feature(ApiFeature.StorageTextureWithoutFormat, Availability.Optional, "needs EXT_shader_image_load_formatted");
}
