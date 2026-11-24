using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 4.6 (GLSL 4.60).</summary>
internal static class OpenGL_46
{
    public static readonly ApiProfile Profile = OpenGL_45.Profile.Next(Target.OpenGL_46, "4.6")
        .Emits("glsl", "460");
}
