// MS-PL License - Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace YotsubaShader.Mcp;

[McpServerToolType]
public sealed class YotsubaTools(McpApi api)
{
    private static readonly JsonSerializerOptions Pretty = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, Pretty);

    [McpServerTool(Name = "search_docs", ReadOnly = true), Description("Full-text search over the Yotsuba Shader reference (language, attributes, compiler, targets, diagnostics). Returns the best pages with a snippet.")]
    public string SearchDocs(
        [Description("Words to look for, e.g. \"uniform buffer\" or \"YS0508\".")] string query,
        [Description("Language of the documentation: \"en\" (default) or \"es\".")] string? lang = null,
        int limit = 8) => Json(api.SearchDocs(query, lang, Math.Clamp(limit, 1, 25)));

    [McpServerTool(Name = "list_docs", ReadOnly = true), Description("Lists every page of the reference with its slug, title and summary. Use the slug with get_doc.")]
    public string ListDocs([Description("\"en\" (default) or \"es\".")] string? lang = null) => Json(api.ListDocs(lang));

    [McpServerTool(Name = "get_doc", ReadOnly = true), Description("Returns the Markdown of a reference page. With `style`, the code tabs of the other styles are left out.")]
    public string GetDoc(
        [Description("Page slug from list_docs, e.g. \"hlsl\", \"targets\", \"validate-vulkan\".")] string slug,
        [Description("\"en\" (default) or \"es\".")] string? lang = null,
        [Description("Syntax (style): hlsl, glsl, msl, vulkan, wgsl. Aliases: spirv, metal, webgpu.")] string? style = null)
    {
        DocPage? doc = api.GetDoc(slug, lang, style);
        return doc is null
            ? throw new McpException($"No page \"{slug}\". Use list_docs to see the slugs.")
            : $"# {doc.Title}\n\nSource: {doc.Url}\n\n{doc.Markdown}";
    }

    [McpServerTool(Name = "list_diagnostics", ReadOnly = true), Description("Lists the analyzer diagnostics (YS0000 codes) with severity. `filter` narrows by code, title or page.")]
    public string ListDiagnostics(string? lang = null, string? filter = null) => Json(api.ListDiagnostics(lang, filter));

    [McpServerTool(Name = "get_diagnostic", ReadOnly = true), Description("Returns the description, message and example shader of a diagnostic such as YS0508.")]
    public string GetDiagnostic([Description("Diagnostic id, e.g. \"YS0508\".")] string code, string? lang = null)
    {
        IReadOnlyList<DiagnosticEntry> found = api.GetDiagnostic(code, lang);
        return found.Count == 0
            ? throw new McpException($"No diagnostic \"{code}\". Use list_diagnostics.")
            : string.Join("\n\n", found.Select(d => d.Markdown));
    }

    [McpServerTool(Name = "list_examples", ReadOnly = true), Description("100 working example shaders, 20 for each style. Filter by `style` and/or `tag` (e.g. raymarching, noise, fractal, gradient).")]
    public string ListExamples(
        [Description("Syntax (style): hlsl, glsl, msl, vulkan, wgsl. Aliases: spirv, metal, webgpu.")] string? style = null,
        string? tag = null) => Json(api.ListExamples(style, tag));

    [McpServerTool(Name = "get_example", ReadOnly = true), Description("Returns the C# source of an example. Every example compiles for all the targets of its style.")]
    public string GetExample(
        [Description("Example id from list_examples, e.g. \"hlsl-plasma\".")] string id,
        [Description("\"en\" (default) or \"es\".")] string? lang = null)
    {
        ExampleEntry example = api.GetExample(id)
            ?? throw new McpException($"No example \"{id}\". Use list_examples.");
        return Render(example, lang);
    }

    [McpServerTool(Name = "search_examples", ReadOnly = true), Description("Finds examples by effect or technique, e.g. \"fractal\", \"water\", \"voronoi\".")]
    public string SearchExamples(string query, string? style = null, int limit = 10) =>
        Json(api.SearchExamples(query, style, Math.Clamp(limit, 1, 25)));

    internal static string Render(ExampleEntry example, string? lang)
    {
        int i = lang == "es" ? 1 : 0;
        string notes = string.IsNullOrEmpty(example.Notes) ? "" : $"\n\nNote: {example.Notes}";
        return $"# {example.Title[i]} ({example.Label})\n\n{example.Description[i]}\n\n{example.Credit}{notes}\n\n```csharp\n{example.Code}\n```";
    }
}

[McpServerResourceType]
public sealed class YotsubaResources(McpApi api)
{
    [McpServerResource(UriTemplate = "yotsuba://docs/{lang}/{slug}", Name = "doc", MimeType = "text/markdown"), Description("A page of the Yotsuba Shader reference.")]
    public string Doc(string lang, string slug)
    {
        DocPage doc = api.GetDoc(slug, lang) ?? throw new McpException($"No page {slug}");
        return $"# {doc.Title}\n\n{doc.Markdown}";
    }

    [McpServerResource(UriTemplate = "yotsuba://examples/{id}", Name = "example", MimeType = "text/x-csharp"), Description("The C# source of an example shader.")]
    public string Example(string id) =>
        (api.GetExample(id) ?? throw new McpException($"No example {id}")).Code;
}

[McpServerPromptType]
public sealed class YotsubaPrompts
{
    [McpServerPrompt(Name = "write_shader"), Description("Asks for a new shader in a given style, grounded on the closest example and the documentation.")]
    public static string WriteShader(
        [Description("One of hlsl, glsl, msl, vulkan, wgsl")] string style,
        [Description("What the shader should draw or compute.")] string goal) =>
        $"Write a Yotsuba Shader in the {style} style that does this: {goal}\n\n" +
        $"First call search_examples and get_example to start from the closest working example, and get_doc for the syntax page of {style}. Return one complete C# file.";
}

public static class McpHost
{
    public const string Instructions =
        "Yotsuba Shader lets you write shaders in C# and compile them to SPIR-V, GLSL, HLSL, MSL and WGSL. " +
        "Search the documentation with search_docs, read a page with get_doc, look up a diagnostic (YS0000) with get_diagnostic, " +
        "and start from a working shader with list_examples / get_example. There are five syntaxes (styles): hlsl, glsl, msl, vulkan (SPIR-V) and wgsl; " +
        "the style only changes how the shader is written, not which backends it compiles to.";

    /// <summary>Registra el servidor MCP (herramientas, recursos y prompt) sobre unos datos concretos.</summary>
    public static IMcpServerBuilder AddYotsubaMcp(this IServiceCollection services, McpData data, string version)
    {
        services.AddSingleton(new McpApi(data));
        return services.AddMcpServer(options =>
            {
                options.ServerInfo = new() { Name = "yotsubashader", Version = version };
                options.ServerInstructions = Instructions;
            })
            .WithTools<YotsubaTools>()
            .WithResources<YotsubaResources>()
            .WithListResourcesHandler((context, _) =>
            {
                McpApi api = context.Services!.GetRequiredService<McpApi>();
                var resources = new List<Resource>();
                foreach (string lang in new[] { "en", "es" })
                {
                    resources.AddRange(api.ListDocs(lang).Select(d => new Resource
                    {
                        Uri = $"yotsuba://docs/{lang}/{d.Slug}", Name = $"{d.Title} ({lang})", Description = d.Summary, MimeType = "text/markdown",
                    }));
                }

                resources.AddRange(api.ListExamples().Select(e => new Resource
                {
                    Uri = $"yotsuba://examples/{e.Id}", Name = $"{e.Title} ({e.Style})", Description = e.Description, MimeType = "text/x-csharp",
                }));
                return ValueTask.FromResult(new ListResourcesResult { Resources = resources });
            })
            .WithPrompts<YotsubaPrompts>();
    }

    /// <summary>`yotsuba mcp`: servidor MCP por stdio. Datos de YOTSUBA_MCP_DATA (una carpeta) o los incrustados.</summary>
    public static async Task<int> RunAsync(string[] args, string version)
    {
        string? directory = Environment.GetEnvironmentVariable("YOTSUBA_MCP_DATA");
        McpData? data = directory is { Length: > 0 } ? McpData.FromDirectory(directory) : McpData.FromEmbedded();
        if (data is null)
        {
            Console.Error.WriteLine(
                "Esta compilación de yotsuba no incluye el contenido del MCP (la referencia y los ejemplos). " +
                "Instala la herramienta desde NuGet o define YOTSUBA_MCP_DATA con la carpeta Web/mcp/data.");
            return 2;
        }

        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddYotsubaMcp(data, version).WithStdioServerTransport();
        await builder.Build().RunAsync();
        return 0;
    }
}
