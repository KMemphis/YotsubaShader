using ShadowDusk.Compiler;
using ShadowDusk.Core;
using ShadowDusk.Core.Preprocessor;
using YotsubaShader.IR.Intermediate;

namespace YotsubaShader.Compiler.Library.CompileToGameEngines;

public static class KNI
{
     public static byte[] ToKNIWebGL_FX(this CompiledCode compiledCode)
    {
        string vsEntry = compiledCode.Reflection.IntermediateLanguage.Functions.First(f => f.Stage == Stage.Vertex).HumanName;
        string psEntry = compiledCode.Reflection.IntermediateLanguage.Functions.First(f => f.Stage == Stage.Fragment).HumanName;

        (string vertexHlsl, string fragmentHlsl) = HlslMerge.Separate(
            compiledCode.ToHlsl(Stage.Vertex), vsEntry,
            compiledCode.ToHlsl(Stage.Fragment), psEntry);

        var files = new Dictionary<string, string>
        {
            ["Shader.vs.hlsl"] = vertexHlsl,
            ["Shader.ps.hlsl"] = fragmentHlsl,
        };

        (string vsProfile, string psProfile) = HlslMerge.ProfilesFor(PlatformTarget.OpenGL);

        string fx = $$"""
                      #include "Shader.vs.hlsl"
                      #include "Shader.ps.hlsl"

                      technique T { pass P { VertexShader = compile {{vsProfile}} {{vsEntry}}(); PixelShader = compile {{psProfile}} {{psEntry}}(); } }
                      """;

        var compiler = new EffectCompiler();

        var result = compiler.Compile(fx, new CompilerOptions
        {
            Target = PlatformTarget.OpenGL,
            SourceFileName = compiledCode.CSharpFullClassName.Split(".").LastOrDefault() + ".fx",
            IncludeResolver = new InMemoryIncludeResolver(files),
        });

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"ShadowDusk no pudo compilar '{compiledCode.CSharpFullClassName}':\n" +
                string.Join('\n', result.Error.Select(error => error.ToString())));
        }

        return result.Value.Data;
    }

}
