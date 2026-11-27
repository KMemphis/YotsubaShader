using System.Text;
using Microsoft.CodeAnalysis;
using YotsubaShader.Rules;
using static YotsubaShader.IDE.Analyzer.Tests.AnalyzerHarness;

namespace YotsubaShader.IDE.Analyzer.Tests;

/// <summary>
/// Un caso por cada <c>Target</c>: el shader más completo que su perfil garantiza debe validar con esa API, y cada
/// característica que esta versión añade respecto a la anterior debe fallar si el shader declara la anterior. Si se
/// añade un miembro a <c>Target</c> sin perfil, <see cref="GraphicsApiTests.Every_target_has_exactly_one_profile"/> falla.
/// </summary>
public class GraphicsApiConformanceTests
{
    /// <summary>Qué escribe un shader para usar una característica. Cada fragmento define sus propios tipos con el sufijo.</summary>
    internal static string Snippet(ApiFeature feature, string n) => feature switch
    {
        ApiFeature.Float64 =>
            $"public class W{n} {{ [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() {{ double d = 1.0; return new float4((float)d); }} }}",
        ApiFeature.Compute =>
            $"public class W{n} {{ [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS() {{ }} }}",
        ApiFeature.StorageBuffer =>
            $"public struct P{n} {{ public float A; }} public class W{n} {{ [register(\"u0\")] public RWStructuredBuffer<P{n}> Items; " +
            $"[ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() {{ return new float4(Items[0].A); }} }}",
        ApiFeature.StorageTexture =>
            $"public class W{n} {{ [register(\"u0\")] public RWTexture2D<float4> Img; " +
            $"[ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() {{ return new float4(1f); }} }}",
        ApiFeature.UnsignedInt =>
            $"public class W{n} {{ [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() {{ uint a = 3u; uint b = a * 2u; return new float4((float)b); }} }}",
        ApiFeature.IntegerBitOps =>
            $"public class W{n} {{ [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() {{ int a = 3; int b = a << 2; return new float4((float)b); }} }}",
        ApiFeature.TexelFetch =>
            $"public class W{n} {{ [register(\"t0\")] public Texture2D Tex; [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() => Tex.Load(new int3(0, 0, 0)); }}",
        ApiFeature.TextureGather =>
            $"public class W{n} {{ [register(\"t0\")] public Texture2D Tex; [register(\"s0\")] public SamplerState Smp; [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() => Tex.Gather(Smp, new float2(0.5f, 0.5f)); }}",
        ApiFeature.TextureOffset =>
            $"public class W{n} {{ [register(\"t0\")] public Texture2D Tex; [register(\"s0\")] public SamplerState Smp; [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() => Tex.Sample(Smp, new float2(0.5f, 0.5f), new int2(1, 1)); }}",
        ApiFeature.TextureQuerySize =>
            $"public class W{n} {{ [register(\"t0\")] public Texture2D Tex; [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS() {{ uint w, h; Tex.GetDimensions(out w, out h); return new float4((float)w); }} }}",
        ApiFeature.Derivatives =>
            $"public class W{n} {{ [ShaderStage(Stage.Fragment)] [return: SV_Target] public float4 FS([TEXCOORD(0)] float x) => new float4(Intrinsic.ddx(x)); }}",
        _ => ""
    };

    /// <summary>Las características que el shader testigo puede usar: no hay forma de escribir las demás.</summary>
    internal static readonly ApiFeature[] Witnessed = { ApiFeature.Float64, ApiFeature.Compute, ApiFeature.StorageBuffer, ApiFeature.StorageTexture,
        ApiFeature.UnsignedInt, ApiFeature.IntegerBitOps, ApiFeature.TexelFetch, ApiFeature.TextureGather, ApiFeature.TextureOffset,
        ApiFeature.TextureQuerySize, ApiFeature.Derivatives };

    /// <summary>El shader más completo que <paramref name="profile"/> garantiza.</summary>
    internal static string MaximalShader(ApiProfile profile)
    {
        var source = new StringBuilder();
        int n = 0;
        foreach (ApiFeature feature in Witnessed)
        {
            if (profile.Support(feature).Level != Availability.Guaranteed) continue;

            // Una textura de almacenamiento sin formato solo vale donde se pueda declarar así.
            if (feature == ApiFeature.StorageTexture && profile.Support(ApiFeature.StorageTextureWithoutFormat).Level == Availability.Unavailable) continue;

            source.AppendLine(Snippet(feature, (n++).ToString()));
        }

        if (n == 0) source.AppendLine(Snippet(ApiFeature.Compute, "Fallback").Replace("Stage.Compute", "Stage.Fragment").Replace("[numthreads(1,1,1)] public void CS() { }", "[return: SV_Target] public float4 FS() { return new float4(1f); }"));
        return source.ToString();
    }

    public static TheoryData<string> Profiles()
    {
        var data = new TheoryData<string>();
        foreach (ApiProfile profile in ApiRegistry.All.Where(p => p.HasBackend)) data.Add(profile.Target.ToString());
        return data;
    }

    private static ApiProfile Find(string name) => ApiRegistry.All.Single(p => p.Target.ToString() == name);

    [Theory]
    [MemberData(nameof(Profiles))]
    public void The_maximal_shader_is_valid_for_its_own_api(string name)
    {
        ApiProfile profile = Find(name);
        var errors = Analyze(MaximalShader(profile), "Target." + name)
            .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        Assert.True(errors.Count == 0, $"{profile.Name}: " + string.Join("; ", errors.Select(d => d.Id + " " + d.GetMessage())));
    }

    public static TheoryData<string, string> Additions()
    {
        var data = new TheoryData<string, string>();
        foreach (ApiProfile profile in ApiRegistry.All)
        {
            if (profile.Previous is not { } previous) continue;
            foreach (ApiFeature feature in Witnessed)
            {
                // El shader testigo declara la textura sin formato: donde el formato es obligatorio no se puede probar así.
                if (feature == ApiFeature.StorageTexture && profile.Support(ApiFeature.StorageTextureWithoutFormat).Level == Availability.Unavailable) continue;

                if (profile.Support(feature).Level == Availability.Guaranteed && previous.Support(feature).Level == Availability.Unavailable)
                {
                    data.Add(profile.Target.ToString(), feature.ToString());
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Additions))]
    public void A_feature_a_version_adds_fails_on_the_previous_one(string name, string featureName)
    {
        ApiProfile profile = Find(name);
        ApiProfile previous = profile.Previous!;
        ApiFeature feature = Enum.Parse<ApiFeature>(featureName);
        string code = Snippet(feature, "0");

        Assert.DoesNotContain(Analyze(code, "Target." + name), d => d.Severity == DiagnosticSeverity.Error);

        var failures = Analyze(code, "Target." + previous.Target).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(failures.Count > 0, $"{feature} debería fallar en {previous.Name} (la añade {profile.Name}).");
    }

    /// <summary>
    /// Con la variable <c>YOTSUBA_API_MATRIX</c> escribe en ese archivo qué espera cada API: el script del CI
    /// (<c>scripts/api_conformance.py</c>) lo lee para saber qué debe funcionar y qué debe fallar en cada versión.
    /// </summary>
    [Fact]
    public void Export_the_matrix_for_the_ci()
    {
        string? path = Environment.GetEnvironmentVariable("YOTSUBA_API_MATRIX");
        if (string.IsNullOrEmpty(path)) return;

        var json = new StringBuilder("[\n");
        bool first = true;
        foreach (ApiProfile profile in ApiRegistry.All)
        {
            if (!first) json.Append(",\n");
            first = false;
            string Features(Func<FeatureSupport, bool> filter) =>
                string.Join(", ", Enum.GetValues<ApiFeature>().Where(f => filter(profile.Support(f))).Select(f => "\"" + f + "\""));

            json.Append("  {")
                .Append($"\"target\": \"{profile.Target}\", \"name\": \"{profile.Name}\", ")
                .Append($"\"previous\": {(profile.Previous is null ? "null" : "\"" + profile.Previous.Target + "\"")}, ")
                .Append($"\"output\": {(profile.OutputTarget is null ? "null" : "\"" + profile.OutputTarget + "\"")}, ")
                .Append($"\"version\": {(profile.OutputVersion is null ? "null" : "\"" + profile.OutputVersion + "\"")}, ")
                .Append($"\"guaranteed\": [{Features(s => s.Level == Availability.Guaranteed)}], ")
                .Append($"\"unavailable\": [{Features(s => s.Level == Availability.Unavailable)}], ")
                .Append($"\"maximal\": \"{string.Join(",", Witnessed.Where(f => profile.Support(f).Level == Availability.Guaranteed))}\"")
                .Append('}');
        }

        json.Append("\n]\n");
        File.WriteAllText(path, json.ToString());
    }
}
