using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Vulkan 1.0 (SPIR-V 1.0). El CLI no genera SPIR-V menor que 1.3, así que ningún backend lo produce.</summary>
internal static class Vulkan_10
{
    public static readonly ApiProfile Profile = ApiProfile.Create(Target.Vulkan_10, "Vulkan", "1.0", ShaderTargets.Vulkan)
        .Feature(ApiFeature.Float64, Availability.Optional, "optional (shaderFloat64)")
        .Feature(ApiFeature.Float16, Availability.Optional, "needs VK_KHR_shader_float16_int8 and shaderFloat16")
        .Feature(ApiFeature.Int16, Availability.Optional, "optional (shaderInt16)")
        .Feature(ApiFeature.Int64, Availability.Optional, "optional (shaderInt64)")
        .Feature(ApiFeature.Compute, Availability.Guaranteed)
        .Feature(ApiFeature.StorageBuffer, Availability.Guaranteed)
        .Feature(ApiFeature.StorageTexture, Availability.Guaranteed)
        .Feature(ApiFeature.StorageTextureWithoutFormat, Availability.Optional, "needs shaderStorageImageReadWithoutFormat/WriteWithoutFormat")
        .Limits(colorAttachments: 4, vertexInputs: 16, interStageVariables: 16, descriptorSets: 4,
            workgroup: new WorkgroupLimits(128, 128, 64, 128));
}
