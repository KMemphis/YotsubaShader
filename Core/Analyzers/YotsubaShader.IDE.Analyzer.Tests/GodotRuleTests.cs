using static YotsubaShader.IDE.Analyzer.Tests.AnalyzerHarness;

namespace YotsubaShader.IDE.Analyzer.Tests;

/// <summary>Con <c>YotsubaShaderTargets=godot</c> se avisa de lo que .gdshader no tiene (YS0710).</summary>
public class GodotRuleTests
{
    public static TheoryData<string> Unsupported => new()
    {
        "public class S { [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS() { } }",
        "public struct P { public float A; } public class S { [register(\"u0\")] public RWStructuredBuffer<P> Items; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }",
        "public class S { [ShaderStage(Stage.Fragment)] public void FS([SV_Target(0)] out float4 a, [SV_Target(1)] out float4 b) { a = new float4(); b = a; } }",
        "public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([YotsubaShader.WgslSyntax.location(0)] float4 p) { return p; } }",
    };

    [Theory]
    [MemberData(nameof(Unsupported))]
    public void Godot_target_warns(string code) => Expect(code, "YS0710", "godot");

    [Theory]
    [MemberData(nameof(Unsupported))]
    public void Other_targets_do_not_warn(string code) => ExpectNone(code, "YS0710", "vulkan");

    [Theory]
    [MemberData(nameof(Unsupported))]
    public void Default_targets_do_not_include_godot(string code) => ExpectNone(code, "YS0710");

    [Fact]
    public void Plain_graphics_shader_is_fine_for_godot() => ExpectNone(
        """
        public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float2 Uv; }
        public class S {
            [DescriptorSet(0)] [Binding(0)] public Texture2D Tex;
            [DescriptorSet(0)] [Binding(1)] public SamplerState Smp;
            [ShaderStage(Stage.Vertex)] public VsOut VS([POSITION] float4 p, [TEXCOORD(0)] float2 uv) { VsOut o; o.Pos = p; o.Uv = uv; return o; }
            [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS(VsOut i) { return Tex.Sample(Smp, i.Uv); }
        }
        """, "YS0710", "godot");
}
