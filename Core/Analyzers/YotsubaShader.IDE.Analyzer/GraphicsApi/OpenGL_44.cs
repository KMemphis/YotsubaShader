using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 4.4 (GLSL 4.40).</summary>
internal static class OpenGL_44
{
    public static readonly ApiProfile Profile = OpenGL_43.Profile.Next(Target.OpenGL_44, "4.4")
        .Emits("glsl", "440");
}
