using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Direct3D 8.1 (Shader Model 1.4). Ningún backend lo genera.</summary>
internal static class DirectX_81
{
    public static readonly ApiProfile Profile = DirectX_80.Profile.Next(Target.DirectX_81, "8.1");
}
