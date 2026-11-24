using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Direct3D 9.0 (Shader Model 2.0). El CLI no genera Shader Model menor que 3.0.</summary>
internal static class DirectX_90
{
    public static readonly ApiProfile Profile = DirectX_81.Profile.Next(Target.DirectX_90, "9.0");
}
