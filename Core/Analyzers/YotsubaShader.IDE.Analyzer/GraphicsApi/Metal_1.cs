using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Metal 1 (MSL 1.x).</summary>
internal static class Metal_1
{
    public static readonly ApiProfile Profile = ApiProfile.Create(Target.Metal_1, "Metal", "1", ShaderTargets.Metal)
        .Emits("msl", "1.0")
        .Feature(ApiFeature.Float64, Availability.Unavailable, "MSL has no double")
        .Feature(ApiFeature.Float16, Availability.Guaranteed)
        .Feature(ApiFeature.Int16, Availability.Guaranteed)
        .Feature(ApiFeature.Compute, Availability.Guaranteed)
        .Feature(ApiFeature.StorageBuffer, Availability.Guaranteed)
        .Feature(ApiFeature.StorageTexture, Availability.Guaranteed)
        .Feature(ApiFeature.StorageTextureWithoutFormat, Availability.Guaranteed)
        .Limits(colorAttachments: 8, vertexInputs: 31);
}
