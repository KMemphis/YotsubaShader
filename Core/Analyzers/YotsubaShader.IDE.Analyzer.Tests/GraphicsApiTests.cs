using Microsoft.CodeAnalysis;
using YotsubaShader.Rules;
using static YotsubaShader.IDE.Analyzer.Tests.AnalyzerHarness;

namespace YotsubaShader.IDE.Analyzer.Tests;

/// <summary>[ShaderTargets] decide contra qué APIs se valida el shader; sin él, Vulkan 1.3.</summary>
public class GraphicsApiTests
{
    private const string Double = "public class S { [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS() { double d = 1.0; } }";
    private const string Compute = "public class S { [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS() { } }";

    private const string StorageBuffer =
        """
        public struct P { public float A; }
        public class S {
            [register("u0")] public RWStructuredBuffer<P> Items;
            [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() { return new float4(Items[0].A); }
        }
        """;

    private static DiagnosticSeverity Severity(string code, string id, string? targets = null) =>
        Expect(code, id, targets).Severity;

    [Fact]
    public void Every_target_has_exactly_one_profile()
    {
        // El Target de la API pública (el del ensamblado de metadatos), no la copia interna del analizador.
        Type targetType = typeof(YotsubaShader.Compiler.Metadata.ShaderTargetsAttribute).Assembly.GetType("YotsubaShader.Compiler.Metadata.Target")!;
        var targets = Enum.GetValues(targetType).Cast<object>()
            .Where(value => System.Numerics.BitOperations.PopCount(Convert.ToUInt64(value)) == 1)
            .Select(value => value.ToString()!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        var profiles = ApiRegistry.All.Select(profile => profile.Target.ToString()).OrderBy(name => name, StringComparer.Ordinal).ToList();

        Assert.Equal(targets, profiles);
        Assert.Equal(profiles.Count, profiles.Distinct().Count());
    }

    [Fact]
    public void Default_is_vulkan_13_and_only_warns() =>
        Assert.Equal(DiagnosticSeverity.Warning, Severity(Double, "YS0701"));

    [Fact]
    public void Optional_feature_is_still_a_warning_when_declared() =>
        Assert.Equal(DiagnosticSeverity.Warning, Severity(Double, "YS0701", "Target.Vulkan_13"));

    [Fact]
    public void Missing_feature_is_an_error_when_declared() =>
        Assert.Equal(DiagnosticSeverity.Error, Severity(Double, "YS0701", "Target.WebGpu"));

    [Theory]
    [InlineData("Target.OpenGL_20", DiagnosticSeverity.Error)]
    [InlineData("Target.OpenGL_31", DiagnosticSeverity.Error)]
    [InlineData("Target.OpenGL_32", DiagnosticSeverity.Warning)]
    [InlineData("Target.OpenGL_33", DiagnosticSeverity.Warning)]
    [InlineData("Target.AllOpenGL", DiagnosticSeverity.Error)]
    [InlineData("Target.OpenGL_40 | Target.OpenGL_46", null)]
    [InlineData("Target.OpenGLES_31", DiagnosticSeverity.Error)]
    public void Double_in_opengl_depends_on_the_version(string targets, DiagnosticSeverity? expected)
    {
        var found = Analyze(Double, targets).FirstOrDefault(d => d.Id == "YS0701");
        Assert.Equal(expected, found?.Severity);
    }

    [Fact]
    public void Missing_feature_names_only_the_declared_apis()
    {
        string message = Expect(Double, "YS0701", "Target.WebGpu | Target.Metal_3").GetMessage();
        Assert.Contains("WebGPU", message);
        Assert.Contains("Metal 3", message);
        Assert.DoesNotContain("Vulkan", message);
    }

    [Fact]
    public void Several_apis_produce_one_diagnostic()
    {
        var all = Analyze(Double, "Target.WebGpu | Target.Metal_3 | Target.OpenGL_33").Where(d => d.Id == "YS0701").ToList();
        Assert.Single(all);
        Assert.Contains("WebGPU", all[0].GetMessage());
        Assert.Contains("OpenGL 3.3", all[0].GetMessage());
    }

    [Fact]
    public void Class_attribute_wins_over_assembly_attribute()
    {
        var diagnostics = Analyze("[ShaderTargets(Target.Vulkan_13)] " + Double, "Target.WebGpu");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostics.First(d => d.Id == "YS0701").Severity);
    }

    [Theory]
    [InlineData("Target.OpenGL_33", true)]
    [InlineData("Target.OpenGL_41", true)]
    [InlineData("Target.OpenGL_43", false)]
    [InlineData("Target.OpenGLES_30", true)]
    [InlineData("Target.OpenGLES_31", false)]
    [InlineData("Target.Vulkan_13", false)]
    [InlineData("Target.DirectX_90c", true)]
    [InlineData("Target.DirectX_11", false)]
    public void Compute_needs_a_version_that_has_it(string targets, bool fails)
    {
        var found = Analyze(Compute, targets).FirstOrDefault(d => d.Id == "YS0711");
        Assert.Equal(fails, found is not null);
        if (fails) Assert.Equal(DiagnosticSeverity.Error, found!.Severity);
    }

    [Fact]
    public void Compute_in_opengl_42_is_only_a_warning_because_an_extension_provides_it()
    {
        var found = Analyze(Compute, "Target.OpenGL_42").Single(d => d.Id == "YS0711");
        Assert.Equal(DiagnosticSeverity.Warning, found.Severity);
        Assert.Contains("ARB_compute_shader", found.GetMessage());
    }

    [Theory]
    [InlineData("Target.OpenGL_42", true)]
    [InlineData("Target.OpenGL_43", false)]
    [InlineData("Target.OpenGLES_30", true)]
    [InlineData("Target.OpenGLES_31", false)]
    public void Storage_buffers_need_a_version_that_has_them(string targets, bool fails) =>
        Assert.Equal(fails, Analyze(StorageBuffer, targets).Any(d => d.Id == "YS0711"));

    [Fact]
    public void Default_target_has_compute_and_storage_buffers()
    {
        ExpectNone(Compute, "YS0711");
        ExpectNone(StorageBuffer, "YS0711");
    }

    [Theory]
    [InlineData("Target.DirectX_80")]
    [InlineData("Target.DirectX_90")]
    [InlineData("Target.Vulkan_13 | Target.DirectX_81")]
    public void A_target_no_backend_generates_is_still_valid(string targets) =>
        ExpectNone("[ShaderTargets(" + targets + ")] " + Compute, "YS0712");

    [Fact]
    public void Empty_declaration_is_the_default() =>
        Assert.Equal(DiagnosticSeverity.Warning, Severity(Double, "YS0701", "Target.None"));

    private const string BitOps = "public class S { [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() { int a = 3; int b = a << 2; return new float4(b); } }";
    private const string Fetch = "public class S { public Texture2D T; [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() { return T.Load(new int3(0, 0, 0)); } }";

    [Theory]
    [InlineData("Target.OpenGL_20", DiagnosticSeverity.Error)]
    [InlineData("Target.OpenGLES_20", DiagnosticSeverity.Error)]
    [InlineData("Target.DirectX_90c", DiagnosticSeverity.Error)]
    [InlineData("Target.DirectX_80", DiagnosticSeverity.Error)]
    [InlineData("Target.OpenGL_30 | Target.OpenGLES_30 | Target.DirectX_10 | Target.Vulkan_10 | Target.Metal_1 | Target.WebGpu", null)]
    public void Bitwise_operators_need_integer_support(string targets, DiagnosticSeverity? expected)
    {
        if (expected is null) ExpectNone(BitOps, "YS0703", targets);
        else Assert.Equal(expected.Value, Severity(BitOps, "YS0703", targets));
    }

    [Theory]
    [InlineData("Target.OpenGLES_20", true)]
    [InlineData("Target.DirectX_90", true)]
    [InlineData("Target.OpenGL_33", false)]
    [InlineData("Target.DirectX_11", false)]
    public void Texel_fetch_needs_integer_texture_addressing(string targets, bool error)
    {
        if (error) Assert.Equal(DiagnosticSeverity.Error, Severity(Fetch, "YS0703", targets));
        else ExpectNone(Fetch, "YS0703", targets);
    }

    private static string Cbuffer(int vectors) =>
        "public struct B { [ArrayLength(" + vectors + ")] public float4[] V; } " +
        "public class S { [register(\"b0\")] public ConstantBuffer<B> Data; [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() { return Data.Value.V[0]; } }";

    [Theory]
    [InlineData(2000, "Target.Vulkan_13", true)]   // 32000 bytes > 16384
    [InlineData(1000, "Target.Vulkan_13", false)]  // 16000 < 16384
    [InlineData(1000, "Target.DirectX_90c", true)] // 16000 > 3584
    [InlineData(3000, "Target.DirectX_11", false)] // 48000 < 65536
    [InlineData(5000, "Target.WebGpu", true)]      // 80000 > 65536
    public void Uniform_buffer_size_is_checked_per_api(int vectors, string targets, bool error)
    {
        bool fits = vectors * 16 <= (targets.Contains("Vulkan") ? 16384 : targets.Contains("90c") ? 3584 : 65536);
        Assert.Equal(!fits, error);
        if (fits) ExpectNone(Cbuffer(vectors), "YS0713", targets);
        else Assert.Equal(DiagnosticSeverity.Error, Severity(Cbuffer(vectors), "YS0713", targets));
    }

    [Fact]
    public void Dynamic_texture_offset_is_not_supported()
    {
        const string code = "public class S { [register(\"t0\")] public Texture2D Tex; [register(\"s0\")] public SamplerState Smp; " +
            "[ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS([TEXCOORD(0)] float2 uv, [TEXCOORD(1)] int k) { return Tex.Sample(Smp, uv, new int2(k, 0)); } }";
        Assert.Equal(DiagnosticSeverity.Error, Severity(code, "YS0508"));
    }

    [Fact]
    public void A_variable_Load_coordinate_is_not_a_texture_offset()
    {
        const string code = "public class S { [register(\"t0\")] public Texture2D Tex; " +
            "[ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS([TEXCOORD(1)] int k) { return Tex.Load(new int3(k, 0, 0)); } }";
        ExpectNone(code, "YS0508");
    }
}
