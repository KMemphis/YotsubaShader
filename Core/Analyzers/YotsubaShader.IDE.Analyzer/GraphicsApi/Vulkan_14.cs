using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Vulkan 1.4 (SPIR-V 1.6).</summary>
internal static class Vulkan_14
{
    public static readonly ApiProfile Profile = Vulkan_13.Profile.Next(Target.Vulkan_14, "1.4")
        .Emits("spirv", "1.6");
}
