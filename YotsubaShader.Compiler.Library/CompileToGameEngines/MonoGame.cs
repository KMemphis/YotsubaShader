using ShadowDusk.Compiler;
using ShadowDusk.Core;
using ShadowDusk.Core.Preprocessor;
using YotsubaShader.IR.Intermediate;

namespace YotsubaShader.Compiler.Library.CompileToGameEngines;

public static class MonoGame
{
    public static byte[] ToMGFX(this CompiledCode compiledCode, MonoGamePlatform platform)
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

        (string Backend, PlatformTarget Target)[] targets =
        [
            ("DesktopGL / GL4 / Android / iOS", PlatformTarget.OpenGL),
            ("WindowsDX", PlatformTarget.DirectX),
            ("WindowsDX12", PlatformTarget.DirectX12),
            ("DesktopVK", PlatformTarget.Vulkan),
        ];

        var compiler = new EffectCompiler();
        foreach (var (backend, target) in targets)
        {

            if (target != ToShadoDuskPlatform(platform)) continue;

            (string vsProfile, string psProfile) = HlslMerge.ProfilesFor(target);

            string fx = $$"""
                          #include "Shader.vs.hlsl"
                          #include "Shader.ps.hlsl"

                          technique T { pass P { VertexShader = compile {{vsProfile}} {{vsEntry}}(); PixelShader = compile {{psProfile}} {{psEntry}}(); } }
                          """;

            var result = compiler.Compile(fx, new CompilerOptions
            {
                Target = target,
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

        throw new PlatformNotSupportedException("MGFX for this platform is not supported");
    }

    private static PlatformTarget ToShadoDuskPlatform(MonoGamePlatform platform)
    {
        switch (platform)
        {
            case MonoGamePlatform.WindowsDX11:
                return PlatformTarget.DirectX;
            case MonoGamePlatform.WindowsDirectX12:
                return PlatformTarget.DirectX12;
            case MonoGamePlatform.DesktopGL:
                return PlatformTarget.OpenGL;
            case MonoGamePlatform.DesktopVulkan:
                return PlatformTarget.Vulkan;
            case MonoGamePlatform.GL4:
                return PlatformTarget.OpenGL;
            case MonoGamePlatform.iOS:
                return PlatformTarget.OpenGL;
            case MonoGamePlatform.Android:
                return PlatformTarget.OpenGL;
        }

        throw new InvalidOperationException("Platform selected is not supported");
    }

}

public enum MonoGamePlatform
{
    DesktopGL,
    WindowsDX11,
    WindowsDirectX12,
    DesktopVulkan,
    iOS,
    Android,
    GL4
}
