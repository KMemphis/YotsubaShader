using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Vulkan 1.1 (SPIR-V 1.3).</summary>
internal static class Vulkan_11
{
    public static readonly ApiProfile Profile = Vulkan_10.Profile.Next(Target.Vulkan_11, "1.1")
        .Emits("spirv", "1.3");
}
