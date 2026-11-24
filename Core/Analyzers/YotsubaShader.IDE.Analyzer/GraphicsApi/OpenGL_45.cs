using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 4.5 (GLSL 4.50).</summary>
internal static class OpenGL_45
{
    public static readonly ApiProfile Profile = OpenGL_44.Profile.Next(Target.OpenGL_45, "4.5")
        .Emits("glsl", "450");
}
