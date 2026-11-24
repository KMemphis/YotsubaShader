using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 3.3 (GLSL 3.30).</summary>
internal static class OpenGL_33
{
    public static readonly ApiProfile Profile = OpenGL_32.Profile.Next(Target.OpenGL_33, "3.3")
        .Emits("glsl", "330");
}
