using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL ES 3.0 (GLSL ES 3.00).</summary>
internal static class OpenGLES_30
{
    public static readonly ApiProfile Profile = OpenGLES_20.Profile.Next(Target.OpenGLES_30, "3.0")
        .Emits("gles", "300")
        .Limits(colorAttachments: 4, vertexInputs: 16, interStageVariables: 15);
}
