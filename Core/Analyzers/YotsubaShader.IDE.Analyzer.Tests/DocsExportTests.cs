using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using YotsubaShader.Rules;
using static YotsubaShader.IDE.Analyzer.Tests.AnalyzerHarness;

namespace YotsubaShader.IDE.Analyzer.Tests;

/// <summary>
/// Con <c>YOTSUBA_DOCS_JSON</c> escribe todo lo que la documentación de learn necesita: el catálogo de diagnósticos,
/// un shader por regla, los perfiles de cada API y, para cada ejemplo de portabilidad, lo que dice el analizador al
/// declarar cada versión. <c>Web/learn/tools/gen-diagnostics-reference.mjs</c> lo convierte en páginas, así que
/// ningún mensaje de la documentación está escrito a mano.
/// </summary>
public class DocsExportTests
{
    /// <summary>Qué escribe un shader para usar algo que no todas las APIs tienen (se muestra tal cual en la documentación).</summary>
    private static readonly (string Key, string Code)[] Examples =
    {
        ("Float64", """
            public class Example
            {
                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS()
                {
                    double d = 1.0;
                    return new float4((float)d);
                }
            }
            """),
        ("Compute", """
            public class Example
            {
                [ShaderStage(Stage.Compute)]
                [numthreads(8, 8, 1)]
                public void CS() { }
            }
            """),
        ("StorageBuffer", """
            public struct Item { public float Value; }

            public class Example
            {
                [register("u0")] public RWStructuredBuffer<Item> Items;

                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS() => new float4(Items[0].Value);
            }
            """),
        ("StorageTexture", """
            public class Example
            {
                [register("u0")] public RWTexture2D<float4> Image;

                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS() => new float4(1f);
            }
            """),
        ("UnsignedInt", """
            public class Example
            {
                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS()
                {
                    uint count = 3u;
                    uint twice = count * 2u;
                    return new float4((float)twice);
                }
            }
            """),
        ("IntegerBitOps", """
            public class Example
            {
                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS()
                {
                    int mask = 3;
                    int shifted = mask << 2;
                    return new float4((float)shifted);
                }
            }
            """),
        ("TexelFetch", """
            public class Example
            {
                [register("t0")] public Texture2D Tex;

                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS() => Tex.Load(new int3(10, 20, 0));
            }
            """),
        ("TextureGather", """
            public class Example
            {
                [register("t0")] public Texture2D Tex;
                [register("s0")] public SamplerState Smp;

                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS() => Tex.Gather(Smp, new float2(0.5f, 0.5f));
            }
            """),
        ("TextureOffset", """
            public class Example
            {
                [register("t0")] public Texture2D Tex;
                [register("s0")] public SamplerState Smp;

                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS([TEXCOORD(0)] float2 uv) => Tex.Sample(Smp, uv, new int2(1, 1));
            }
            """),
        ("TextureQuerySize", """
            public class Example
            {
                [register("t0")] public Texture2D Tex;

                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS()
                {
                    uint width, height;
                    Tex.GetDimensions(out width, out height);
                    return new float4((float)width, (float)height, 0f, 1f);
                }
            }
            """),
        ("Derivatives", """
            public class Example
            {
                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS([TEXCOORD(0)] float x) => new float4(Intrinsic.ddx(x));
            }
            """),
        ("UniformBuffer", """
            public struct Palette
            {
                [ArrayLength(1100)] public float4[] Colors;   // 17 600 bytes
            }

            public class Example
            {
                [register("b0")] public ConstantBuffer<Palette> Data;

                [ShaderStage(Stage.Fragment)]
                [return: SV_Target]
                public float4 FS() => Data.Value.Colors[0];
            }
            """),
        ("Workgroup", """
            public class Example
            {
                [ShaderStage(Stage.Compute)]
                [numthreads(256, 1, 1)]   // 256 invocations
                public void CS() { }
            }
            """),
    };

    private static object Diagnostics(string code, string? targets) =>
        Analyze(code, targets).Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d => new { id = d.Id, severity = d.Severity.ToString().ToLowerInvariant(), message = d.GetMessage() }).ToList();

    private static object Export(string culture)
    {
        CultureInfo info = CultureInfo.GetCultureInfo(culture);
        CultureInfo.DefaultThreadCurrentUICulture = info;
        CultureInfo.CurrentUICulture = info;

        var rules = ShaderRules.All.OrderBy(d => d.Id, StringComparer.Ordinal).Select(d => new
        {
            id = d.Id,
            title = d.Title.ToString(info),
            message = d.MessageFormat.ToString(info),
            severity = d.DefaultSeverity.ToString().ToLowerInvariant(),
            category = d.Category
        }).ToList();

        var cases = new List<object>();
        foreach (object[] row in RuleTests.Cases)
        {
            string id = (string)row[0], code = (string)row[1];
            var found = Analyze(code).Where(d => d.Id == id).Select(d => new { severity = d.Severity.ToString().ToLowerInvariant(), message = d.GetMessage() }).FirstOrDefault();
            if (found is not null) cases.Add(new { id, code, found.severity, found.message });
        }

        var profiles = ApiRegistry.All.Select(profile => new
        {
            target = profile.Target.ToString(),
            name = profile.Name,
            api = profile.Api,
            version = profile.Version,
            hasBackend = profile.HasBackend,
            features = Enum.GetValues<ApiFeature>().ToDictionary(f => f.ToString(), f => new
            {
                level = profile.Support(f).Level.ToString(),
                note = profile.Support(f).Note is { } note ? Loc.T(note, info) : null
            }),
            limits = new
            {
                colorAttachments = profile.MaxColorAttachments,
                vertexInputs = profile.MaxVertexInputs,
                interStageVariables = profile.MaxInterStageVariables,
                descriptorSets = profile.MaxDescriptorSets,
                uniformBufferBytes = profile.MaxUniformBufferBytes,
                workgroup = profile.Workgroup is { } w ? new { w.X, w.Y, w.Z, w.Invocations } : null
            }
        }).ToList();

        var examples = Examples.Select(example => new
        {
            key = example.Key,
            code = example.Code,
            results = ApiRegistry.All.ToDictionary(p => p.Target.ToString(), p => Diagnostics(example.Code, "Target." + p.Target))
        }).ToList();

        return new { culture, rules, cases, profiles, examples };
    }

    [Fact]
    public void Export_the_documentation_data()
    {
        string? path = Environment.GetEnvironmentVariable("YOTSUBA_DOCS_JSON");
        if (string.IsNullOrEmpty(path)) return;

        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            foreach (string culture in new[] { "en", "es" })
            {
                File.WriteAllText(Path.Combine(path, culture + ".json"), JsonSerializer.Serialize(Export(culture), options));
            }
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = null;
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
