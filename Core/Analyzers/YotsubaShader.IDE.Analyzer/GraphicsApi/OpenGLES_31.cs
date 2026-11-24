using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL ES 3.1 (GLSL ES 3.10): llegan compute, storage buffers e imágenes.</summary>
internal static class OpenGLES_31
{
    public static readonly ApiProfile Profile = OpenGLES_30.Profile.Next(Target.OpenGLES_31, "3.1")
        .Emits("gles", "310")
        .Feature(ApiFeature.Compute, Availability.Guaranteed)
        .Feature(ApiFeature.StorageBuffer, Availability.Guaranteed)
        .Feature(ApiFeature.StorageTexture, Availability.Guaranteed)
        .Limits(workgroup: new WorkgroupLimits(128, 128, 64, 128));
}
