using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 3.1 (GLSL 1.40).</summary>
internal static class OpenGL_31
{
    public static readonly ApiProfile Profile = OpenGL_30.Profile.Next(Target.OpenGL_31, "3.1")
        .Emits("glsl", "140");
}
