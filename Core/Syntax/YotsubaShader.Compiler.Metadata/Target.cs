using System;

namespace YotsubaShader.Compiler.Metadata
{

    [Flags]
    public enum Target : ulong
    {
        None = 0UL,
        Vulkan_10 = 1UL << 0,
        Vulkan_11 = 1UL << 1,
        Vulkan_12 = 1UL << 2,
        Vulkan_13 = 1UL << 3,
        Vulkan_14 = 1UL << 4,
        OpenGL_20 = 1UL << 5,
        OpenGL_30 = 1UL << 6,
        OpenGL_31 = 1UL << 7,
        OpenGL_32 = 1UL << 8,
        OpenGL_33 = 1UL << 9,
        OpenGL_40 = 1UL << 10,
        OpenGL_41 = 1UL << 11,
        OpenGL_42 = 1UL << 12,
        OpenGL_43 = 1UL << 13,
        OpenGL_44 = 1UL << 14,
        OpenGL_45 = 1UL << 15,
        OpenGL_46 = 1UL << 16,
        OpenGLES_20 = 1UL << 17,
        OpenGLES_30 = 1UL << 18,
        OpenGLES_31 = 1UL << 19,
        OpenGLES_32 = 1UL << 20,
        DirectX_80 = 1UL << 21,
        DirectX_81 = 1UL << 22,
        DirectX_90 = 1UL << 23,
        DirectX_90c = 1UL << 24,
        DirectX_10 = 1UL << 25,
        DirectX_11 = 1UL << 26,
        DirectX_12 = 1UL << 27,
        Metal_1 = 1UL << 28,
        Metal_2 = 1UL << 29,
        Metal_3 = 1UL << 30,
        Metal_4 = 1UL << 31,
        WebGpu = 1UL << 32,
        AllVulkan = Vulkan_10 | Vulkan_11 | Vulkan_12 | Vulkan_13 | Vulkan_14,

        AllOpenGL = OpenGL_20 | OpenGL_30 | OpenGL_31 | OpenGL_32 | OpenGL_33 |
                    OpenGL_40 | OpenGL_41 | OpenGL_42 | OpenGL_43 | OpenGL_44 | OpenGL_45 | OpenGL_46,
        AllOpenGLES = OpenGLES_20 | OpenGLES_30 | OpenGLES_31 | OpenGLES_32,
        AllDirectX = DirectX_80 | DirectX_81 | DirectX_90 | DirectX_90c | DirectX_10 | DirectX_11 | DirectX_12,
        AllMetal = Metal_1 | Metal_2 | Metal_3 | Metal_4,
        All = AllVulkan | AllDirectX | AllMetal | AllOpenGL | AllOpenGLES | WebGpu
    }

}