// MS-PL License - Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic
using YotsubaShader.Mcp;

namespace YotsubaShader.Mcp.Tests;

public class McpApiTests
{
    private static readonly McpApi Api = new(Load());

    private static McpData Load() =>
        McpData.FromEmbedded() ?? throw new InvalidOperationException(
            "Faltan los datos del MCP: clona el repo de la web en Web/ y ejecuta " +
            "`node Web/learn/tools/gen-mcp-data.mjs` y `node Web/mcp/tools/build-examples.mjs`.");

    [Fact]
    public void Has_twenty_examples_per_style()
    {
        Assert.Equal(100, Api.ListExamples().Count);
        foreach (string style in McpApi.Styles)
        {
            Assert.Equal(20, Api.ListExamples(style).Count);
        }
    }

    [Theory]
    [InlineData("spirv", "vulkan")]
    [InlineData("Metal", "msl")]
    [InlineData("webgpu", "wgsl")]
    [InlineData("hlsl", "hlsl")]
    public void Style_aliases_are_normalized(string alias, string expected) =>
        Assert.Equal(expected, McpApi.NormalizeStyle(alias));

    [Fact]
    public void Every_example_has_code_and_both_languages()
    {
        foreach (ExampleEntry example in Api.Data.Examples)
        {
            Assert.Contains("class", example.Code);
            Assert.Equal(2, example.Title.Length);
            Assert.Equal(2, example.Description.Length);
        }
    }

    [Fact]
    public void Playground_shaders_are_included() =>
        Assert.All(new[] { "aurora", "bricks", "fractal", "mandelbulb", "mesh-gradient", "orbs", "pool" },
            name => Assert.Contains(Api.Data.Examples, e => e.Style == "glsl" && e.Id == $"glsl-{name}"));

    [Fact]
    public void Docs_exist_in_both_languages()
    {
        Assert.NotEmpty(Api.ListDocs("en"));
        Assert.Equal(Api.ListDocs("en").Count, Api.ListDocs("es").Count);
        Assert.Contains(Api.ListDocs("es"), d => d.Slug == "mcp");
    }

    [Fact]
    public void Search_finds_pages_and_diagnostics()
    {
        Assert.NotEmpty(Api.SearchDocs("uniform"));
        Assert.Contains(Api.SearchDocs("YS0508"), h => h.Title.StartsWith("YS0508"));
        Assert.Empty(Api.SearchDocs("   "));
    }

    [Fact]
    public void Diagnostics_are_found_by_code_case_insensitively()
    {
        Assert.NotEmpty(Api.GetDiagnostic("ys0508"));
        Assert.Empty(Api.GetDiagnostic("YS9999"));
        Assert.NotEmpty(Api.ListDiagnostics(filter: "resources"));
    }

    [Fact]
    public void GetDoc_with_style_drops_the_other_styles()
    {
        DocPage page = Api.GetDoc("hlsl", "en")!;
        DocPage only = Api.GetDoc("hlsl", "en", "wgsl")!;
        Assert.True(only.Markdown.Length <= page.Markdown.Length);
        Assert.All(only.Blocks, b => Assert.True(b.Style is null or "wgsl"));
        Assert.Null(Api.GetDoc("no-such-page"));
    }

    [Fact]
    public void Search_examples_respects_the_style_filter()
    {
        var hits = Api.SearchExamples("plasma", "glsl");
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal("glsl", h.Style));
    }

    [Fact]
    public void Tools_render_examples_and_report_missing_ones()
    {
        var tools = new YotsubaTools(Api);
        Assert.Contains("```csharp", tools.GetExample("hlsl-plasma"));
        Assert.Throws<ModelContextProtocol.McpException>(() => tools.GetExample("nope"));
        Assert.Throws<ModelContextProtocol.McpException>(() => tools.GetDiagnostic("YS9999"));
    }
}
