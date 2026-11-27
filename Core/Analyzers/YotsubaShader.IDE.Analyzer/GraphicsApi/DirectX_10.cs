using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Direct3D 10 (Shader Model 4.0).</summary>
internal static class DirectX_10
{
    public static readonly ApiProfile Profile = DirectX_90c.Profile.Next(Target.DirectX_10, "10")
        .Emits("hlsl", "4.0")
        .Feature(ApiFeature.Compute, Availability.Optional, "cs_4_0 only on Direct3D 10 hardware that supports it")
        .Limits(colorAttachments: 8, interStageVariables: 16)
        .Feature(ApiFeature.UnsignedInt, Availability.Guaranteed)
        .Feature(ApiFeature.IntegerBitOps, Availability.Guaranteed)
        .Feature(ApiFeature.TexelFetch, Availability.Guaranteed)
        .Feature(ApiFeature.TextureOffset, Availability.Guaranteed)
        .Feature(ApiFeature.TextureQuerySize, Availability.Guaranteed)
        .Feature(ApiFeature.TextureGather, Availability.Optional, "Gather4 needs Shader Model 4.1 (Direct3D 10.1)")
        .Limits(uniformBufferBytes: 65536);
}
