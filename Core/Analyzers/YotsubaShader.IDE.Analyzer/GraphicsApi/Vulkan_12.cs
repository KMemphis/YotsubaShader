using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Vulkan 1.2 (SPIR-V 1.5): shaderFloat16 pasa al núcleo.</summary>
internal static class Vulkan_12
{
    public static readonly ApiProfile Profile = Vulkan_11.Profile.Next(Target.Vulkan_12, "1.2")
        .Emits("spirv", "1.5")
        .Feature(ApiFeature.Float16, Availability.Optional, "optional (shaderFloat16)");
}
