using static YotsubaShader.IDE.Analyzer.Tests.AnalyzerHarness;

namespace YotsubaShader.IDE.Analyzer.Tests;

/// <summary>Shaders correctos: el analizador no debe dar ningún error.</summary>
public class ValidShaderTests
{
    public static TheoryData<string> Shaders => new()
    {
        "public class S { [ShaderStage(Stage.Fragment)] public float4 FS() { return new float4(); } }",
        "public class S { [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS([SV_Position] float4 p) { return p; } }",
        "public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([POSITION] float4 p, [SV_VertexID] uint id) { return p; } }",
        "public class S { [ShaderStage(Stage.Compute)] [numthreads(8, 8, 1)] public void CS([SV_DispatchThreadID] uint3 id, [SV_GroupIndex] uint index) { } }",
        "public class S { [ShaderStage(Stage.Fragment)] public void FS([SV_Target(0)] out float4 a, [SV_Target(1)] out float4 b, [SV_Depth] out float d) { a = new float4(); b = a; d = 0.5f; } }",
        """
        public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float2 Uv; }
        public class S {
            [DescriptorSet(0)] [Binding(0)] public Texture2D Tex;
            [DescriptorSet(0)] [Binding(1)] public SamplerState Smp;
            [ShaderStage(Stage.Vertex)] public VsOut VS([POSITION] float4 p, [TEXCOORD(0)] float2 uv) { VsOut o; o.Pos = p; o.Uv = uv; return o; }
            [ShaderStage(Stage.Fragment)] public float4 FS(VsOut i) { return Tex.Sample(Smp, i.Uv); }
        }
        """,
        """
        public struct Params { public float4 Color; public float Time; }
        public class S {
            [Uniform] [DescriptorSet(0)] [Binding(0)] public Params Globals;
            [ShaderStage(Stage.Fragment)] public float4 FS() { return Globals.Color; }
        }
        """,
        "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] float x) { return (float)System.Math.Sqrt(x) + System.MathF.Sin(x); } }",
        "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] float x) { float y = x; for (int i = 0; i < 4; i++) { y += Helper(y); } return y; } float Helper(float v) { return v * 0.5f; } }",
        "public class S { [ShaderStage(Stage.Fragment)] public float FS() { float[] a = new float[3]; foreach (float v in a) { } return a.Length; } }",
        "public class S { [ShaderStage(Stage.Fragment)] public float FS() { float x = 1f; Inc(ref x); Get(out float y); return x + y; } void Inc(ref float x) { x += 1f; } void Get(out float y) { y = 2f; } }",
        "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] int n) { switch (n) { case 0: return 1f; default: return n switch { 1 => 2f, _ => 3f }; } } }",
        "public class S { [ShaderStage(Stage.Fragment)] public float FS() { float f = default; return f; } }",
        "public class S { [ShaderStage(Stage.Fragment)] public float FS([TEXCOORD(0)] float x) { return System.MathF.Cbrt(x) + (float)System.Math.Round((double)x, 2) + System.MathF.CopySign(x, -1f); } }",
        "public class S { [ShaderStage(Stage.Fragment)] public float FS() { var (a, b) = (1f, 2f); (a, b) = (b, a); return a; } }",
        "public class S { [ShaderStage(Stage.Fragment)] public float FS() { float t = 0f; void Add(float v) { t += v; } Add(1f); return t; } }",
        """
        public struct Particle { public float4 Position; public uint Count; }
        public struct Camera { public float4x4 ViewProjection; public float Time; }
        public class S {
            [register("t0")] public StructuredBuffer<Particle> Input;
            [register("u0")] public RWStructuredBuffer<Particle> Output;
            [register("b0")] public ConstantBuffer<Camera> Cam;
            [ShaderStage(Stage.Compute)] [numthreads(64, 1, 1)]
            public void CS([SV_DispatchThreadID] uint3 id)
            {
                int i = (int)id.X;
                if (i >= Input.Length) return;
                Particle p = Input[i];
                p.Position.X += Cam.Value.Time;
                Output[i] = p;
                Intrinsic.InterlockedAdd(ref Output[i].Count, 1u);
            }
        }
        """,
        """
        public struct Particle { public float4 Position; public uint Hits; }
        public class S {
            [DescriptorSet(0)] [Binding(0)] public System.Collections.Immutable.ImmutableArray<Particle> Input;
            [DescriptorSet(0)] [Binding(1)] public Particle[] Output;
            [DescriptorSet(0)] [Binding(2)] public System.Collections.Generic.List<uint> Counters;
            [ShaderStage(Stage.Compute)] [numthreads(64, 1, 1)]
            public void CS([SV_DispatchThreadID] uint3 id)
            {
                int i = (int)id.X;
                if (i >= Input.Length) return;
                float t = 0f;
                foreach (Particle q in Input) t += q.Position.X;
                Particle p = Input[i];
                p.Position.X = t;
                Output[i] = p;
                Counters[i] = Counters[i] + 1u;
                Intrinsic.InterlockedAdd(ref Output[i].Hits, 1u);
            }
        }
        """,
        "public class S { const float Scale = 2f; [ShaderStage(Stage.Fragment)] public float FS() { return Scale; } }",
    };

    [Theory]
    [MemberData(nameof(Shaders))]
    public void Valid_shader_has_no_errors(string code) => ExpectNoErrors(code);
}
