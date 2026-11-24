using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL ES 3.2 (GLSL ES 3.20).</summary>
internal static class OpenGLES_32
{
    public static readonly ApiProfile Profile = OpenGLES_31.Profile.Next(Target.OpenGLES_32, "3.2")
        .Emits("gles", "320");
}
