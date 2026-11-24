using System.Collections.Generic;
using System.Linq;
using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Todos los perfiles, uno por miembro de <see cref="Target"/> que representa una API y versión.</summary>
internal static class ApiRegistry
{
    public static IReadOnlyList<ApiProfile> All { get; } = new[]
    {
        Vulkan_10.Profile, Vulkan_11.Profile, Vulkan_12.Profile, Vulkan_13.Profile, Vulkan_14.Profile,
        OpenGL_20.Profile, OpenGL_30.Profile, OpenGL_31.Profile, OpenGL_32.Profile, OpenGL_33.Profile,
        OpenGL_40.Profile, OpenGL_41.Profile, OpenGL_42.Profile, OpenGL_43.Profile, OpenGL_44.Profile,
        OpenGL_45.Profile, OpenGL_46.Profile,
        OpenGLES_20.Profile, OpenGLES_30.Profile, OpenGLES_31.Profile, OpenGLES_32.Profile,
        DirectX_80.Profile, DirectX_81.Profile, DirectX_90.Profile, DirectX_90c.Profile, DirectX_10.Profile,
        DirectX_11.Profile, DirectX_12.Profile,
        Metal_1.Profile, Metal_2.Profile, Metal_3.Profile, Metal_4.Profile,
        WebGpu.Profile
    };

    /// <summary>Los perfiles de las banderas activas en <paramref name="declared"/>, de la versión más antigua a la más nueva.</summary>
    public static IReadOnlyList<ApiProfile> For(Target declared) =>
        All.Where(profile => (declared & profile.Target) != 0).ToList();

    /// <summary>Los bits de <paramref name="declared"/> que no corresponden a ningún perfil.</summary>
    public static Target Unknown(Target declared)
    {
        Target known = Target.None;
        foreach (ApiProfile profile in All) known |= profile.Target;
        return declared & ~known;
    }
}
