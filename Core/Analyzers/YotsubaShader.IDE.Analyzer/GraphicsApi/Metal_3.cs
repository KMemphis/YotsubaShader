using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Metal 3 (MSL 3.x).</summary>
internal static class Metal_3
{
    public static readonly ApiProfile Profile = Metal_2.Profile.Next(Target.Metal_3, "3")
        .Emits("msl", "3.0");
}
