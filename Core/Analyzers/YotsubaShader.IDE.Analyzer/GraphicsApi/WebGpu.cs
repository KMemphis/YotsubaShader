using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>WebGPU (WGSL).</summary>
internal static class WebGpu
{
    public static readonly ApiProfile Profile = ApiProfile.Create(Target.WebGpu, "WebGPU", "1.0", ShaderTargets.WebGPU)
        .Emits("wgsl", null)
        .Feature(ApiFeature.Float64, Availability.Unavailable, "does not exist in WGSL")
        .Feature(ApiFeature.Int64, Availability.Unavailable, "does not exist in WGSL")
        .Feature(ApiFeature.Int16, Availability.Unavailable, "does not exist in WGSL")
        .Feature(ApiFeature.Float16, Availability.Optional, "needs the 'shader-f16' feature")
        .Feature(ApiFeature.Compute, Availability.Guaranteed)
        .Feature(ApiFeature.StorageBuffer, Availability.Guaranteed)
        .Feature(ApiFeature.StorageTexture, Availability.Guaranteed)
        .Feature(ApiFeature.StorageTextureWithoutFormat, Availability.Unavailable, "WGSL requires the texture format")
        .Limits(colorAttachments: 8, vertexInputs: 16, interStageVariables: 16, descriptorSets: 4,
            workgroup: new WorkgroupLimits(256, 256, 64, 256))
        .Feature(ApiFeature.UnsignedInt, Availability.Guaranteed)
        .Feature(ApiFeature.IntegerBitOps, Availability.Guaranteed)
        .Feature(ApiFeature.TexelFetch, Availability.Guaranteed)
        .Feature(ApiFeature.TextureGather, Availability.Guaranteed)
        .Feature(ApiFeature.TextureOffset, Availability.Guaranteed)
        .Feature(ApiFeature.TextureQuerySize, Availability.Guaranteed)
        .Feature(ApiFeature.Derivatives, Availability.Guaranteed)
        .Limits(uniformBufferBytes: 65536);
}
