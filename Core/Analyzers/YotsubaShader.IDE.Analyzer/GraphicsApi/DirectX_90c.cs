using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Direct3D 9.0c (Shader Model 3.0).</summary>
internal static class DirectX_90c
{
    public static readonly ApiProfile Profile = DirectX_90.Profile.Next(Target.DirectX_90c, "9.0c")
        .Emits("hlsl", "3.0")
        .Limits(colorAttachments: 4, vertexInputs: 16)
        .Limits(uniformBufferBytes: 3584);
}
