using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Direct3D 11 (Shader Model 5.0): llegan compute, UAV y double.</summary>
internal static class DirectX_11
{
    public static readonly ApiProfile Profile = DirectX_10.Profile.Next(Target.DirectX_11, "11")
        .Emits("hlsl", "5.0")
        .Feature(ApiFeature.Float64, Availability.Optional, "optional (D3D11_FEATURE_DOUBLES)")
        .Feature(ApiFeature.Compute, Availability.Guaranteed)
        .Feature(ApiFeature.StorageBuffer, Availability.Guaranteed)
        .Feature(ApiFeature.StorageTexture, Availability.Guaranteed)
        .Feature(ApiFeature.StorageTextureWithoutFormat, Availability.Optional, "loading typed UAVs without a format is optional")
        .Limits(vertexInputs: 32, interStageVariables: 32)
        .Feature(ApiFeature.TextureGather, Availability.Guaranteed);
}
