using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Vulkan 1.3 (SPIR-V 1.6). Es el destino por defecto cuando el shader no declara [ShaderTargets].</summary>
internal static class Vulkan_13
{
    public static readonly ApiProfile Profile = Vulkan_12.Profile.Next(Target.Vulkan_13, "1.3")
        .Emits("spirv", "1.6");
}
