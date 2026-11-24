using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>OpenGL 4.0 (GLSL 4.00): llega double.</summary>
internal static class OpenGL_40
{
    public static readonly ApiProfile Profile = OpenGL_33.Profile.Next(Target.OpenGL_40, "4.0")
        .Emits("glsl", "400")
        .Feature(ApiFeature.Float64, Availability.Guaranteed);
}
