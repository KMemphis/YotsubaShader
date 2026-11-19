using static YotsubaShader.IDE.Analyzer.Tests.AnalyzerHarness;

namespace YotsubaShader.IDE.Analyzer.Tests;

/// <summary>Cada regla con un shader que la dispara. Los shaders válidos están en <see cref="ValidShaderTests"/>.</summary>
public class RuleTests
{
    public static TheoryData<string, string> Cases => new()
    {
        // ── Puntos de entrada ──
        { "YS0101", "public class S { [ShaderStage(Stage.Compute)] public void CS() { } }" },
        { "YS0102", "public class S { [ShaderStage(Stage.Compute)] [numthreads(0, 1, 1)] public void CS() { } }" },
        { "YS0104", "public class S { [ShaderStage(Stage.Compute)] [numthreads(64, 64, 1)] public void CS() { } }" },
        { "YS0104", "public class S { [ShaderStage(Stage.Compute)] [numthreads(1, 1, 65)] public void CS() { } }" },
        { "YS0105", "public class S { [ShaderStage(Stage.Fragment)] [numthreads(8, 8, 1)] public float4 FS() { return new float4(); } }" },
        { "YS0100", "public class S { [ShaderStage(Stage.Fragment)] public T FS<T>() { return default(T); } }" },
        { "YS0107", "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] ref float x) { return x; } }" },
        { "YS0108", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] float x) { return x; } }" },
        { "YS0103", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { return Helper(); } float Helper() { return FS(); } }" },

        // ── Interfaz ──
        { "YS0201", "public class S { [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS([SV_Position] float4 p) { } }" },
        { "YS0201", "public class S { [ShaderStage(Stage.Fragment)] public float FS([SV_VertexID] uint id) { return 1f; } }" },
        { "YS0201", "public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([SV_DispatchThreadID] uint3 id) { return new float4(); } }" },
        { "YS0201", "public class S { [ShaderStage(Stage.Vertex)] public void VS([SV_Target(0)] out float4 c) { c = new float4(); } }".Replace("SV_Target(0)", "SV_Depth") },
        { "YS0202", "public class S { [ShaderStage(Stage.Fragment)] [return: SV_Position] public float4 FS([SV_Position] float4 p) { return p; } }" },
        { "YS0202", "public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([SV_Position] float4 p) { return p; } }" },
        { "YS0203", "public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float3 VS([POSITION] float3 p) { return p; } }" },
        { "YS0203", "public class S { [ShaderStage(Stage.Fragment)] public float FS([SV_IsFrontFace] uint f) { return 1f; } }" },
        { "YS0203", "public class S { [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS([SV_DispatchThreadID] uint id) { } }" },
        { "YS0204", "public class S { [ShaderStage(Stage.Vertex)] public void VS([SV_Position] out float4 a, [SV_Position] out float4 b) { a = new float4(); b = new float4(); } }" },
        { "YS0204", "public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([TEXCOORD(0)] float4 a, [TEXCOORD(0)] float4 b) { return a; } }" },
        { "YS0205", "public class S { [ShaderStage(Stage.Fragment)] public void FS([SV_Target(1)] out float a, [SV_Target(1)] out float b) { a = 1f; b = 2f; } }" },
        { "YS0206", "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] [SV_Position] float4 p) { return 1f; } }" },
        { "YS0207", "public class S { [ShaderStage(Stage.Fragment)] public float FS(float x) { return x; } }" },
        { "YS0207", "public struct VsOut { [SV_Position] public float4 Pos; public float2 Uv; } public class S { [ShaderStage(Stage.Vertex)] public VsOut VS([POSITION] float4 p) { VsOut o; o.Pos = p; o.Uv = new float2(); return o; } }" },
        { "YS0208", "public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([POSITION] float4 p, [TEXCOORD(0)] bool flag) { return p; } }" },
        { "YS0209", "public class S { [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS([TEXCOORD(0)] float x) { } }" },
        { "YS0210", "public class S { [ShaderStage(Stage.Fragment)] [return: SV_Target(8)] public float4 FS() { return new float4(); } }" },
        { "YS0211", "public class S { [ShaderStage(Stage.Fragment)] [return: SV_Target(-1)] public float FS() { return 1f; } }" },
        { "YS0212", "public class S { [ShaderStage(Stage.Fragment)] [return: TEXCOORD(0)] public float4 FS() { return new float4(); } }" },
        { "YS0213", "public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([POSITION] float4 p, [TEXCOORD(0)] float[] w) { return p; } }" },
        { "YS0214", "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] Texture2D t) { return 1f; } }" },
        { "YS0217", "public class S { [ShaderStage(Stage.Fragment)] public float FS([Binding(0)] float x) { return x; } }" },
        { "YS0218", "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] decimal x) { return 1f; } }" },
        { "YS0219", "public class S { [ShaderStage(Stage.Fragment)] public float FS([SV_Barycentrics] float3 b) { return 1f; } }" },
        { "YS0220", "public struct Inner { public float A; } public struct Outer { [SV_Position] public float4 P; public Inner I; } public class S { [ShaderStage(Stage.Vertex)] public Outer VS([POSITION] float4 p) { Outer o; o.P = p; o.I.A = 1f; return o; } }" },
        { "YS0221", "public class S { [ShaderStage(Stage.Vertex)] [return: TEXCOORD(0)] public float4 VS([POSITION] float4 p) { return p; } }" },
        { "YS0215", """
            public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float2 Uv; }
            public struct PsIn { [SV_Position] public float4 Pos; [TEXCOORD(1)] public float2 Uv; }
            public class S {
                [ShaderStage(Stage.Vertex)] public VsOut VS([POSITION] float4 p) { VsOut o; o.Pos = p; o.Uv = new float2(); return o; }
                [ShaderStage(Stage.Fragment)] public float4 FS(PsIn i) { return i.Pos; }
            }
            """ },
        { "YS0216", """
            public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float2 Uv; }
            public struct PsIn { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float3 Uv; }
            public class S {
                [ShaderStage(Stage.Vertex)] public VsOut VS([POSITION] float4 p) { VsOut o; o.Pos = p; o.Uv = new float2(); return o; }
                [ShaderStage(Stage.Fragment)] public float4 FS(PsIn i) { return i.Pos; }
            }
            """ },

        // ── Recursos ──
        { "YS0301", "public class S { public Texture2D Tex; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0302", "public class S { [DescriptorSet(0)] [Binding(1)] public Texture2D A; [DescriptorSet(0)] [Binding(1)] public SamplerState B; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0304", "public class S { [register(\"s0\")] public Texture2D Tex; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0305", "public class S { [Uniform] [DescriptorSet(0)] [Binding(0)] public float Time; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0306", "public struct P { public float4 C; } public class S { [Uniform] [DescriptorSet(0)] [Binding(0)] public P Globals { get; set; } [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0307", "public struct P { public bool Flag; } public class S { [Uniform] [DescriptorSet(0)] [Binding(0)] public P Globals; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0309", "public class S { [DescriptorSet(0)] [Binding(0)] [TEXCOORD(0)] public Texture2D Tex; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0310", "public class S { public float Scale; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0505", "public class S { static float counter; [ShaderStage(Stage.Fragment)] public float FS() { counter += 1f; return counter; } }" },

        // ── Métodos y C# no soportado ──
        { "YS0401", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { return Id(1f); } T Id<T>(T x) { return x; } }" },
        { "YS0403", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { return Sum(1f, 2f); } float Sum(params float[] v) { return v[0]; } }" },
        { "YS0404", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { return F(3); } float F(int n) { return n <= 0 ? 0f : F(n - 1); } }" },
        { "YS0404", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { return A(3); } float A(int n) { return B(n); } float B(int n) { return A(n); } }" },
        { "YS0406", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { return float.Parse(\"1\"); } }" },
        { "YS0406", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { return System.Math.BigMul(2, 3); } }" },
        { "YS0406", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { return (float)System.Math.BitIncrement(1.0); } }" },
        { "YS0501", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { try { return 1f; } catch { return 0f; } } }" },
        { "YS0501", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { throw new System.Exception(); } }" },
        { "YS0501", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { int i = 0; again: i++; if (i < 3) goto again; return i; } }" },
        { "YS0502", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { System.Func<float> f = () => 1f; return 1f; } }" },
        { "YS0503", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { string s = \"a\"; return 1f; } }" },
        { "YS0503", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { decimal d = 1m; return 1f; } }" },
        { "YS0503", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { float? x = null; return 1f; } }" },
        { "YS0503", "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] uint a, [TEXCOORD(1)] int b) { return a + b; } }" },
        { "YS0308", "public struct D { public float[] V; } public class S { [YotsubaShader.WgslSyntax.Resources.storage(YotsubaShader.WgslSyntax.Resources.storage_use.read)] [DescriptorSet(0)] [Binding(0)] public D Data; [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS() { Data.V[0] = 1f; } }" },
        { "YS0201", "public class S { [SV_DispatchThreadID] public uint3 Id; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0209", "public class S { [TEXCOORD(0)] public float4 Uv; [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS() { } }" },
        { "YS0502", "public class S { [ShaderStage(Stage.Fragment)] public unsafe float FS() { float x = 1f; float* p = &x; return *p; } }" },
        { "YS0307", "public class S { [DescriptorSet(0)] [Binding(0)] public StructuredBuffer<bool> Flags; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0305", "public class S { [DescriptorSet(0)] [Binding(0)] public ConstantBuffer<float> Time; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0301", "public struct P { public float A; } public class S { public StructuredBuffer<P> Items; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0307", "public class S { [DescriptorSet(0)] [Binding(0)] public System.Collections.Immutable.ImmutableArray<bool> Flags; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0301", "public class S { public float[] Data; [ShaderStage(Stage.Fragment)] public float FS() { return 1f; } }" },
        { "YS0406", "public class S { [DescriptorSet(0)] [Binding(0)] public System.Collections.Generic.List<float> Data; [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS() { Data.Add(1f); } }" },
        { "YS0503", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { var l = new System.Collections.Generic.List<float>(); return 1f; } }" },
        { "YS0504", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { new float4(); return 1f; } }" },
        { "YS0507", "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] int n) { float[] a = new float[n]; return 1f; } }" },
        { "YS0507", "public class S { [ShaderStage(Stage.Fragment)] public float FS() { float[,] a = new float[2, 2]; return 1f; } }" },

        // ── Uso por etapa ──
        { "YS0601", "public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([POSITION] float4 p) { float d = Intrinsic.ddx(p.X); return p; } }" },
        { "YS0601", "public class S { [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS() { Intrinsic.clip(1f); } }" },
        { "YS0601", "public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([POSITION] float4 p) { Helper(p.X); return p; } void Helper(float x) { Intrinsic.clip(x); } }" },
        { "YS0603", "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] float x) { x = 2f; return x; } }" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Rule_fires(string id, string code) => Expect(code, id);
}
