using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Metal 2 (MSL 2.x).</summary>
internal static class Metal_2
{
    public static readonly ApiProfile Profile = Metal_1.Profile.Next(Target.Metal_2, "2")
        .Emits("msl", "2.0");
}
