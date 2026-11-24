using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Direct3D 12 (Shader Model 6.x).</summary>
internal static class DirectX_12
{
    public static readonly ApiProfile Profile = DirectX_11.Profile.Next(Target.DirectX_12, "12")
        .Emits("hlsl", "6.0")
        .Feature(ApiFeature.Int64, Availability.Optional, "needs Shader Model 6.0 and 64-bit support in the hardware")
        .Feature(ApiFeature.Float16, Availability.Optional, "needs Shader Model 6.2 with -enable-16bit-types")
        .Feature(ApiFeature.Int16, Availability.Optional, "needs Shader Model 6.2 with -enable-16bit-types");
}
