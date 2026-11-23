using System.Text;
using YotsubaShader.Compiler.SpirV;
using YotsubaShader.Extensions;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.Roslyn;

namespace YotsubaShader.Example;

/// <summary>El SPIR-V público no lleva SPV_GOOGLE_*; el HLSL sigue saliendo con las semánticas.</summary>
public class CleanSpirvTests
{
    private const string Shader = """
        public struct VsIn { [POSITION] public float4 Pos; [COLOR] public float4 Color; }
        public struct VsOut { [SV_Position] public float4 Pos; [COLOR] public float4 Color; }
        public class S {
            [ShaderStage(Stage.Vertex)] public VsOut VS(VsIn i) { VsOut o; o.Pos = i.Pos; o.Color = i.Color; return o; }
            [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS(VsOut i) { return i.Color; }
        }
        """;

    private static CompiledCode Compile()
    {
        var compilation = TestCompilation.Create(SpirvPipeline.Header + Shader);
        var (name, ir) = Assert.Single(new ShaderProcessor().Process(compilation, CancellationToken.None));
        var module = new IRToSpirvModule().Compile(ir);
        return new CompiledCode(new ShaderReflection(ir), new SpirVCompiler().Compile(module), module.ToSpirVAs, name);
    }

    [Fact]
    public void Public_spirv_has_no_google_extensions()
    {
        CompiledCode code = Compile();

        Assert.DoesNotContain("SPV_GOOGLE", Encoding.ASCII.GetString(code.SpirV));
        Assert.DoesNotContain("SPV_GOOGLE", Encoding.ASCII.GetString(code.ToSpirV(1, 3)));
        Assert.Equal((1, 6), SpirvVersion.Read(code.SpirV));
    }

    [Fact]
    public void Hlsl_keeps_the_semantics()
    {
        string hlsl = Compile().ToHlsl(Stage.Vertex);

        Assert.Contains("POSITION", hlsl);
        Assert.Contains("COLOR", hlsl);
    }
}
