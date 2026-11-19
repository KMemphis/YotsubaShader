using System.Diagnostics;
using System.Text;
using YotsubaShader.Compiler.SpirV;
using YotsubaShader.Extensions;
using YotsubaShader.Extensions.Godot;
using YotsubaShader.Roslyn;

namespace YotsubaShader.Example;

/// <summary>
/// C# → SPIR-V → .gdshader. El texto se comprueba aquí; que Godot lo compila de verdad lo comprueba
/// <see cref="Generated_shaders_compile_in_godot"/> con el binario de Godot si está instalado (variable GODOT o /Applications).
/// </summary>
public class GodotTests
{
    private const string VertexAndFragment = """
        public struct VsIn { [POSITION] public float4 Pos; [NORMAL] public float3 N; [TEXCOORD(0)] public float2 Uv; }
        public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float2 Uv; [TEXCOORD(1)] public float3 N; }
        public struct Params { public float4 Tint; public float Time; }
        public class S {
            [DescriptorSet(0)] [Binding(0)] public Texture2D Tex;
            [DescriptorSet(0)] [Binding(1)] public SamplerState Smp;
            [Uniform] [DescriptorSet(0)] [Binding(2)] public Params P;
            [ShaderStage(Stage.Vertex)] public VsOut VS(VsIn i) { VsOut o; o.Pos = i.Pos; o.Uv = i.Uv; o.N = i.N; return o; }
            float Helper(float x) { return x * 0.5f; }
            [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS(VsOut i) {
                float4 c = Tex.Sample(Smp, i.Uv); c.X = c.X * P.Tint.X; c.W = Helper(P.Time); return c;
            }
        }
        """;

    private static CompiledCode Compile(string code)
    {
        var compilation = TestCompilation.Create(SpirvPipeline.Header + code);
        var (name, ir) = Assert.Single(new ShaderProcessor().Process(compilation, CancellationToken.None));
        var module = new IRToSpirvModule().Compile(ir);
        return new CompiledCode(new ShaderReflection(ir), new SpirVCompiler().Compile(module), module.ToSpirVAs, name);
    }

    private static string Translate(string code, GodotShaderType type) =>
        Encoding.UTF8.GetString(new GodotTargetBackend().Compile(Compile(code), (uint)type));

    [Fact]
    public void Spatial_maps_the_stages_to_vertex_and_fragment()
    {
        string gdshader = Translate(VertexAndFragment, GodotShaderType.Spatial);

        Assert.Contains("shader_type spatial;", gdshader);
        Assert.Contains("render_mode unshaded, cull_disabled;", gdshader);
        Assert.Contains("uniform sampler2D Tex;", gdshader);
        Assert.Contains("uniform vec4 P_Tint;", gdshader);
        Assert.Contains("uniform float P_Time;", gdshader);
        Assert.Contains("varying vec2 v_Uv;", gdshader);
        Assert.Contains("vec4 i_Pos = vec4(VERTEX, 1.0);", gdshader);
        Assert.Contains("vec3 i_N = NORMAL;", gdshader);
        Assert.Contains("vec2 i_Uv = UV;", gdshader);
        Assert.Contains("POSITION = ys_position;", gdshader);
        Assert.Contains("texture(Tex, v_Uv)", gdshader);
        Assert.Contains("ALBEDO = ys_color.rgb; ALPHA = ys_color.a;", gdshader);
        Assert.Contains("float Helper(float x)", gdshader);
        Assert.DoesNotContain("layout", gdshader);
        Assert.DoesNotContain("#version", gdshader);
        Assert.DoesNotContain("gl_", gdshader);
        Assert.DoesNotContain("sampler(", gdshader);
    }

    [Fact]
    public void Canvas_item_writes_COLOR_and_VERTEX()
    {
        string gdshader = Translate("""
            public struct VsIn { [POSITION] public float4 Pos; [TEXCOORD(0)] public float2 Uv; [COLOR] public float4 Tint; }
            public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float2 Uv; [COLOR] public float4 Tint; }
            public class S {
                [DescriptorSet(0)] [Binding(0)] public Texture2D Tex;
                [DescriptorSet(0)] [Binding(1)] public SamplerState Smp;
                [ShaderStage(Stage.Vertex)] public VsOut VS(VsIn i) { VsOut o; o.Pos = i.Pos; o.Uv = i.Uv; o.Tint = i.Tint; return o; }
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS(VsOut i) { float4 c = Tex.Sample(Smp, i.Uv); c.X = c.X * i.Tint.X; return c; }
            }
            """, GodotShaderType.CanvasItem);

        Assert.Contains("shader_type canvas_item;", gdshader);
        Assert.DoesNotContain("render_mode", gdshader);
        Assert.Contains("vec4 i_Pos = vec4(VERTEX, 0.0, 1.0);", gdshader);
        Assert.Contains("VERTEX = ys_position.xy;", gdshader);
        Assert.Contains("COLOR = ys_color;", gdshader);
        Assert.DoesNotContain("ALBEDO", gdshader);
    }

    [Fact]
    public void Early_return_still_writes_the_color()
    {
        string gdshader = Translate("""
            public class S {
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS([SV_Position] float4 p) {
                    if (p.X > 10f) { return new float4(); }
                    float4 c = new float4(); c.X = 1f; return c;
                }
            }
            """, GodotShaderType.Spatial);

        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(gdshader, "ALBEDO = ys_color.rgb;").Count);
        Assert.Contains("FRAGCOORD", gdshader);
        Assert.DoesNotContain("gl_FragCoord", gdshader);
    }

    [Fact]
    public void Unused_resources_are_not_declared()
    {
        string gdshader = Translate("""
            public class S {
                [DescriptorSet(0)] [Binding(0)] public Texture2D Unused;
                [DescriptorSet(0)] [Binding(1)] public SamplerState Smp;
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() { return new float4(); }
            }
            """, GodotShaderType.Spatial);

        Assert.DoesNotContain("Unused", gdshader);
        Assert.DoesNotContain("Smp", gdshader);
    }

    public static TheoryData<string, string, GodotShaderType> Rejected => new()
    {
        { "no tiene etapas vertex ni fragment", "public class S { [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS() { } }", GodotShaderType.Spatial },
        { "storage buffers", """
            public struct P { public float A; }
            public class S { [register("u0")] public RWStructuredBuffer<P> Items;
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() { float4 c = new float4(); c.X = Items[0].A; return c; } }
            """, GodotShaderType.Spatial },
        { "render targets múltiples", "public class S { [ShaderStage(Stage.Fragment)] public void FS([SV_Target(0)] out float4 a, [SV_Target(1)] out float4 b) { a = new float4(); b = a; } }", GodotShaderType.Spatial },
        { "NORMAL0", """
            public struct VsIn { [POSITION] public float4 Pos; [NORMAL] public float3 N; }
            public class S { [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS(VsIn i) { return i.Pos; } }
            """, GodotShaderType.CanvasItem },
        { "vertex no la escribe", """
            public struct FsIn { [TEXCOORD(0)] public float2 Uv; }
            public class S { [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS(FsIn i) { return new float4(); } }
            """, GodotShaderType.Spatial },
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Unsupported_shaders_throw_a_clear_error(string reason, string code, GodotShaderType type)
    {
        var exception = Assert.Throws<NotSupportedException>(() => Translate(code, type));
        Assert.Contains(reason, exception.Message);
    }

    /// <summary>Shaders de gráficos variados: cada uno se traduce a spatial y, cuando tiene sentido, a canvas_item.</summary>
    public static TheoryData<string, string, bool> Corpus => new()
    {
        { "textured", VertexAndFragment, false },
        { "flat-color", """
            public class S {
                [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([POSITION] float4 p) { return p; }
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() { float4 c = new float4(); c.X = 1f; c.W = 1f; return c; }
            }
            """, true },
        { "fragment-only", """
            public class S {
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS([SV_Position] float4 p) {
                    float4 c = new float4(); c.X = p.X * 0.01f; c.Y = p.Y * 0.01f; c.W = 1f; return c;
                }
            }
            """, true },
        { "loops-switch-locals", """
            public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public float2 Uv; }
            public class S {
                [ShaderStage(Stage.Vertex)] public VsOut VS([POSITION] float4 p, [TEXCOORD(0)] float2 uv) { VsOut o; o.Pos = p; o.Uv = uv; return o; }
                float Fold(float x) { float s = 0f; for (int k = 0; k < 4; k++) { s += x * k; } return s; }
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS(VsOut i) {
                    int n = (int)(i.Uv.X * 3f);
                    float v = 0f;
                    switch (n) { case 0: v = 0.25f; break; case 1: v = Fold(i.Uv.Y); break; default: v = 1f; break; }
                    float Twice(float a) { return a * 2f; }
                    float4 c = new float4(); c.X = Twice(v); c.W = 1f; return c;
                }
            }
            """, true },
        { "ref-params-and-tuples", """
            public class S {
                void Bump(ref float x) { x += 1f; }
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS([SV_Position] float4 p) {
                    float x = p.X; Bump(ref x); var (a, b) = (x, p.Y); float4 c = new float4(); c.X = a; c.Y = b; c.W = 1f; return c;
                }
            }
            """, true },
        { "flat-integer-varying", """
            public struct VsOut { [SV_Position] public float4 Pos; [TEXCOORD(0)] public uint Id; }
            public class S {
                [ShaderStage(Stage.Vertex)] public VsOut VS([POSITION] float4 p, [SV_VertexID] uint id) { VsOut o; o.Pos = p; o.Id = id; return o; }
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS(VsOut i) { float4 c = new float4(); c.X = i.Id * 0.1f; c.W = 1f; return c; }
            }
            """, true },
        { "derivatives-and-discard", """
            public class S {
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS([SV_Position] float4 p) {
                    Intrinsic.clip(p.X - 4f);
                    float4 c = new float4(); c.X = Intrinsic.ddx(p.X); c.W = 1f; return c;
                }
            }
            """, true },
        { "uniform-block-and-math", """
            public struct Params { public float4 Tint; public float Time; public float Scale; }
            public class S {
                [Uniform] [DescriptorSet(0)] [Binding(0)] public Params G;
                [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([POSITION] float4 p) { float4 q = p; q.X = q.X * G.Scale; return q; }
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS([SV_Position] float4 p) {
                    float4 c = G.Tint; c.X = System.MathF.Sin(G.Time) * 0.5f + 0.5f; return c;
                }
            }
            """, true },
        { "front-facing-and-depth", """
            public class S {
                [ShaderStage(Stage.Fragment)] public void FS([SV_Position] float4 p, [SV_IsFrontFace] bool front, [SV_Target] out float4 color, [SV_Depth] out float depth) {
                    color = new float4(); color.X = front ? 1f : 0f; color.W = 1f; depth = 0.5f;
                }
            }
            """, false },
        { "instance-id", """
            public class S {
                [ShaderStage(Stage.Vertex)] [return: SV_Position] public float4 VS([POSITION] float4 p, [SV_InstanceID] uint i) { float4 q = p; q.X += i; return q; }
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() { float4 c = new float4(); c.W = 1f; return c; }
            }
            """, true },
        { "float3-output", """
            public class S {
                [ShaderStage(Stage.Fragment)] [return: SV_Target] public float3 FS([SV_Position] float4 p) { float3 c = new float3(); c.X = p.X; return c; }
            }
            """, true },
    };

    [Theory]
    [MemberData(nameof(Corpus))]
    public void Corpus_translates(string name, string code, bool alsoCanvasItem)
    {
        CompiledCode compiled = Compile(code);
        var backend = new GodotTargetBackend();
        Assert.Contains("shader_type spatial;", Encoding.UTF8.GetString(backend.Compile(compiled, 0)));
        if (alsoCanvasItem)
        {
            Assert.Contains("shader_type canvas_item;", Encoding.UTF8.GetString(backend.Compile(compiled, 1)));
        }
    }

    [Fact]
    public void Generated_shaders_compile_in_godot()
    {
        string? godot = FindGodot();
        if (godot is null) return; // sin Godot instalado solo se comprueba el texto

        string directory = Path.Combine(Path.GetTempPath(), "ys_godot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var files = new List<string>();
        try
        {
            foreach (object[] row in Corpus)
            {
                string name = (string)row[0];
                CompiledCode compiled = Compile((string)row[1]);
                var backend = new GodotTargetBackend();
                string spatial = Path.Combine(directory, name + ".spatial.gdshader");
                File.WriteAllBytes(spatial, backend.Compile(compiled, 0));
                files.Add(spatial);
                if ((bool)row[2])
                {
                    string canvas = Path.Combine(directory, name + ".canvasitem.gdshader");
                    File.WriteAllBytes(canvas, backend.Compile(compiled, 1));
                    files.Add(canvas);
                }
            }

            string text = RunGodot(godot, files);
            Assert.Contains("YOTSUBA_CHECK done", text);
            Assert.DoesNotContain("SHADER ERROR", text);
            Assert.DoesNotContain("Shader compilation failed", text);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string RunGodot(string godot, List<string> files)
    {
        string project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../Plugins/YotsubaShader.Extensions.Godot/tools/shader-check"));
        var start = new ProcessStartInfo(godot)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("--path");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("--script");
        start.ArgumentList.Add("check.gd");
        start.ArgumentList.Add("--");
        foreach (string file in files) start.ArgumentList.Add(file);

        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd() + error.Result;
        Assert.True(process.WaitForExit(120_000), "Godot no terminó");
        return output;
    }

    private static string? FindGodot()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("GODOT");
        if (!string.IsNullOrEmpty(fromEnvironment) && File.Exists(fromEnvironment)) return fromEnvironment;

        string mac = "/Applications/Godot.app/Contents/MacOS/Godot";
        return File.Exists(mac) ? mac : null;
    }
}
