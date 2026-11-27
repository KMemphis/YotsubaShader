using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL ES 2.0 (GLSL ES 1.00).</summary>
internal static class OpenGLES_20
{
    public static readonly ApiProfile Profile = ApiProfile.Create(Target.OpenGLES_20, "OpenGL ES", "2.0", ShaderTargets.OpenGL)
        .Emits("gles", "100")
        .Feature(ApiFeature.Float64, Availability.Unavailable, "OpenGL ES has no double")
        .Feature(ApiFeature.Float16, Availability.Optional, "needs an extension")
        .Feature(ApiFeature.Int16, Availability.Optional, "needs an extension")
        .Feature(ApiFeature.Int64, Availability.Optional, "needs an extension")
        .Feature(ApiFeature.Compute, Availability.Unavailable, "compute shaders arrive in OpenGL ES 3.1")
        .Feature(ApiFeature.StorageBuffer, Availability.Unavailable, "storage buffers arrive in OpenGL ES 3.1")
        .Feature(ApiFeature.StorageTexture, Availability.Unavailable, "images (load/store) arrive in OpenGL ES 3.1")
        .Feature(ApiFeature.StorageTextureWithoutFormat, Availability.Unavailable, "GLSL ES requires the image format")
        .Limits(colorAttachments: 1, vertexInputs: 8, interStageVariables: 8)
        .Feature(ApiFeature.UnsignedInt, Availability.Optional, "GLSL ES 1.00 has no uint; the generated shader emulates it with int")
        .Feature(ApiFeature.IntegerBitOps, Availability.Unavailable, "needs GLSL ES 3.00 (OpenGL ES 3.0)")
        .Feature(ApiFeature.TexelFetch, Availability.Unavailable, "needs GLSL ES 3.00 (OpenGL ES 3.0)")
        .Feature(ApiFeature.TextureOffset, Availability.Unavailable, "needs GLSL ES 3.00 (OpenGL ES 3.0)")
        .Feature(ApiFeature.TextureQuerySize, Availability.Unavailable, "needs GLSL ES 3.00 (OpenGL ES 3.0)")
        .Feature(ApiFeature.TextureGather, Availability.Unavailable, "textureGather arrives in OpenGL ES 3.1")
        .Feature(ApiFeature.Derivatives, Availability.Optional, "needs OES_standard_derivatives");
}
