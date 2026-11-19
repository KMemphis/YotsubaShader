using System;
using System.Collections.Generic;

namespace YotsubaShader.Rules;

/// <summary>APIs gráficas a las que se traduce un shader. Las alertas de portabilidad solo nombran las elegidas.</summary>
[Flags]
internal enum ShaderTargets
{
    None = 0,
    Vulkan = 1 << 0,
    D3D12 = 1 << 1,
    Metal = 1 << 2,
    WebGPU = 1 << 3,
    OpenGL = 1 << 4,

    /// <summary>Godot Shading Language. No forma parte de <see cref="All"/>: solo se valida si se pide con <c>godot</c>.</summary>
    Godot = 1 << 5,
    All = Vulkan | D3D12 | Metal | WebGPU | OpenGL
}

internal static class ShaderTargetsParser
{
    /// <summary>Propiedad MSBuild <c>YotsubaShaderTargets</c>, p. ej. <c>Vulkan;Metal</c>. Vacía = todos.</summary>
    public const string MSBuildProperty = "build_property.YotsubaShaderTargets";

    public static ShaderTargets Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ShaderTargets.All;

        ShaderTargets result = ShaderTargets.None;
        foreach (string part in value!.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            result |= part.Trim().ToLowerInvariant() switch
            {
                "vulkan" or "spirv" or "spir-v" => ShaderTargets.Vulkan,
                "d3d12" or "d3d" or "directx" or "dx12" or "hlsl" => ShaderTargets.D3D12,
                "metal" or "msl" => ShaderTargets.Metal,
                "webgpu" or "wgsl" => ShaderTargets.WebGPU,
                "opengl" or "gl" or "glsl" or "gles" => ShaderTargets.OpenGL,
                "godot" or "gdshader" => ShaderTargets.Godot,
                "all" => ShaderTargets.All,
                _ => ShaderTargets.None
            };
        }

        return result == ShaderTargets.None ? ShaderTargets.All : result;
    }

    /// <summary>"WebGPU, Metal" para el mensaje de una alerta.</summary>
    public static string Describe(ShaderTargets targets)
    {
        var names = new List<string>();
        if ((targets & ShaderTargets.Vulkan) != 0) names.Add("Vulkan");
        if ((targets & ShaderTargets.D3D12) != 0) names.Add("Direct3D 12");
        if ((targets & ShaderTargets.Metal) != 0) names.Add("Metal");
        if ((targets & ShaderTargets.WebGPU) != 0) names.Add("WebGPU");
        if ((targets & ShaderTargets.OpenGL) != 0) names.Add("OpenGL");
        if ((targets & ShaderTargets.Godot) != 0) names.Add("Godot");
        return string.Join(", ", names);
    }
}
