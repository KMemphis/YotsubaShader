using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Direct3D 9.0 (Shader Model 2.0). El CLI no genera Shader Model menor que 3.0.</summary>
internal static class DirectX_90
{
    public static readonly ApiProfile Profile = DirectX_81.Profile.Next(Target.DirectX_90, "9.0")
        .Feature(ApiFeature.UnsignedInt, Availability.Optional, "Shader Model 2/3 emulates integers with floats")
        .Feature(ApiFeature.IntegerBitOps, Availability.Unavailable, "bitwise operations need Shader Model 4.0 (Direct3D 10)")
        .Feature(ApiFeature.Derivatives, Availability.Guaranteed)
        .Limits(uniformBufferBytes: 512);
}
