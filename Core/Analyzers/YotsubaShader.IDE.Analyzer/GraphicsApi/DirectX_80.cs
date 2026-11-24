using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Direct3D 8.0 (Shader Model 1.x). Ningún backend lo genera.</summary>
internal static class DirectX_80
{
    public static readonly ApiProfile Profile = ApiProfile.Create(Target.DirectX_80, "Direct3D", "8.0", ShaderTargets.D3D12)
        .Feature(ApiFeature.Float64, Availability.Unavailable, "HLSL only has double since Shader Model 5.0")
        .Feature(ApiFeature.Int64, Availability.Unavailable, "HLSL only has 64-bit integers since Shader Model 6.0")
        .Feature(ApiFeature.Compute, Availability.Unavailable, "compute shaders arrive in Shader Model 4.0 (Direct3D 10)")
        .Feature(ApiFeature.StorageBuffer, Availability.Unavailable, "read/write buffers (UAV) arrive in Shader Model 5.0 (Direct3D 11)")
        .Feature(ApiFeature.StorageTexture, Availability.Unavailable, "read/write textures (UAV) arrive in Shader Model 5.0 (Direct3D 11)");
}
