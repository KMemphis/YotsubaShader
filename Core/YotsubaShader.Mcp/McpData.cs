// MS-PL License - Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YotsubaShader.Mcp;

public sealed record DocBlock(string? Lang, string? Style, string Code);

public sealed record DocPage(string Slug, string Title, string Summary, int Order, string Url, string Markdown, List<DocBlock> Blocks);

public sealed record DiagnosticEntry(string Code, string Title, string Severity, string Message, string Page, string Markdown);

public sealed record DocSet(string Lang, List<DocPage> Docs, List<DiagnosticEntry> Diagnostics);

public sealed record ExampleEntry(
    string Id, string Style, string[] Title, string[] Description, string[] Tags,
    string Credit, string Source, string Code, string Label, string? Notes);

/// <summary>El contenido que sirve el MCP: la referencia de learn (en/es) y los 100 ejemplos.</summary>
public sealed class McpData
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public required IReadOnlyDictionary<string, DocSet> Docs { get; init; }
    public required IReadOnlyList<ExampleEntry> Examples { get; init; }

    /// <summary>Carga los datos de una carpeta con docs-en.json, docs-es.json y examples.json.</summary>
    public static McpData FromDirectory(string directory) => Load(name => File.OpenRead(Path.Combine(directory, name)));

    /// <summary>Carga los datos incrustados en el ensamblado; null si esta compilación no los incluye.</summary>
    public static McpData? FromEmbedded()
    {
        Assembly assembly = typeof(McpData).Assembly;
        if (assembly.GetManifestResourceInfo("mcp-data/examples.json") is null)
        {
            return null;
        }

        return Load(name => assembly.GetManifestResourceStream($"mcp-data/{name}")
            ?? throw new FileNotFoundException($"Falta el recurso incrustado mcp-data/{name}."));
    }

    private static McpData Load(Func<string, Stream> open)
    {
        T Read<T>(string name)
        {
            using Stream stream = open(name);
            return JsonSerializer.Deserialize<T>(stream, Options)
                ?? throw new InvalidDataException($"{name} está vacío.");
        }

        return new McpData
        {
            Docs = new Dictionary<string, DocSet>
            {
                ["en"] = Read<DocSet>("docs-en.json"),
                ["es"] = Read<DocSet>("docs-es.json"),
            },
            Examples = Read<List<ExampleEntry>>("examples.json"),
        };
    }
}
