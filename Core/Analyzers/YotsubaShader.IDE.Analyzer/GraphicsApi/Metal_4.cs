using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>Metal 4 (MSL 4.x).</summary>
internal static class Metal_4
{
    public static readonly ApiProfile Profile = Metal_3.Profile.Next(Target.Metal_4, "4")
        .Emits("msl", "4.0");
}
