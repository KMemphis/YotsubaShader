using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 4.1 (GLSL 4.10).</summary>
internal static class OpenGL_41
{
    public static readonly ApiProfile Profile = OpenGL_40.Profile.Next(Target.OpenGL_41, "4.1")
        .Emits("glsl", "410");
}
