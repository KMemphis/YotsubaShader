// MS-PL License - Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic
using System.Text.RegularExpressions;

namespace YotsubaShader.Mcp;

public sealed record DocSummary(string Slug, string Title, string Summary, string Url);
public sealed record DocHit(string Slug, string Title, string? Url, int Score, string Snippet);
public sealed record DiagnosticSummary(string Code, string Title, string Severity, string Page);
public sealed record ExampleSummary(string Id, string Style, string Title, string Description, string[] Tags);

/// <summary>Las consultas detrás de cada herramienta: funciones puras sobre <see cref="McpData"/>, compartidas con las pruebas.</summary>
public sealed partial class McpApi(McpData data)
{
    public static readonly string[] Styles = ["hlsl", "glsl", "msl", "vulkan", "wgsl"];

    private static readonly Dictionary<string, string> StyleAliases = new()
    {
        ["spirv"] = "vulkan", ["spir-v"] = "vulkan", ["metal"] = "msl", ["webgpu"] = "wgsl",
        ["direct3d"] = "hlsl", ["opengl"] = "glsl",
    };

    public McpData Data => data;

    public static string? NormalizeStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return null;
        }

        string key = style.ToLowerInvariant();
        return Styles.Contains(key) ? key : StyleAliases.GetValueOrDefault(key);
    }

    private DocSet Set(string? lang) => data.Docs.TryGetValue(lang ?? "en", out DocSet? set) ? set : data.Docs["en"];

    private static string[] Words(string query) =>
        WordSplit().Split(query.ToLowerInvariant()).Where(w => w.Length > 1).ToArray();

    private static int Score(string text, string[] terms)
    {
        int total = 0;
        foreach (string term in terms)
        {
            int count = 0;
            int at = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            while (at != -1 && count < 20)
            {
                count++;
                at = text.IndexOf(term, at + term.Length, StringComparison.OrdinalIgnoreCase);
            }

            total += count;
        }

        return total;
    }

    private static string Snippet(string text, string[] terms, int size = 220)
    {
        int at = -1;
        foreach (string term in terms)
        {
            at = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (at != -1)
            {
                break;
            }
        }

        if (at == -1)
        {
            return Collapse(text[..Math.Min(size, text.Length)]);
        }

        int from = Math.Max(0, at - 60);
        string piece = Collapse(text.Substring(from, Math.Min(size, text.Length - from)));
        return (from > 0 ? "…" : "") + piece + "…";
    }

    private static string Collapse(string text) => Whitespace().Replace(text, " ");

    public IReadOnlyList<DocSummary> ListDocs(string? lang = null) =>
        Set(lang).Docs.Select(d => new DocSummary(d.Slug, d.Title, d.Summary, d.Url)).ToList();

    public DocPage? GetDoc(string slug, string? lang = null, string? style = null)
    {
        DocPage? doc = Set(lang).Docs.FirstOrDefault(d => d.Slug == slug);
        string? wanted = NormalizeStyle(style);
        if (doc is null || wanted is null)
        {
            return doc;
        }

        string markdown = CodeFence().Replace(doc.Markdown, match =>
            match.Groups["style"].Value == wanted ? match.Value : "");
        return doc with
        {
            Markdown = markdown,
            Blocks = doc.Blocks.Where(b => b.Style is null || b.Style == wanted).ToList(),
        };
    }

    public IReadOnlyList<DocHit> SearchDocs(string query, string? lang = null, int limit = 8)
    {
        string[] terms = Words(query);
        var hits = new List<DocHit>();
        if (terms.Length == 0)
        {
            return hits;
        }

        DocSet set = Set(lang);
        foreach (DocPage doc in set.Docs)
        {
            int score = Score(doc.Title, terms) * 8 + Score(doc.Summary, terms) * 3 + Score(doc.Markdown, terms);
            if (score > 0)
            {
                hits.Add(new DocHit(doc.Slug, doc.Title, doc.Url, score, Snippet(doc.Markdown, terms)));
            }
        }

        foreach (DiagnosticEntry diagnostic in set.Diagnostics)
        {
            int score = Score($"{diagnostic.Code} {diagnostic.Title}", terms) * 8 + Score(diagnostic.Message, terms) * 2;
            if (score > 0)
            {
                string? url = set.Docs.FirstOrDefault(d => d.Slug == diagnostic.Page)?.Url;
                hits.Add(new DocHit(diagnostic.Page, $"{diagnostic.Code}: {diagnostic.Title}", url, score, diagnostic.Message));
            }
        }

        return hits.OrderByDescending(h => h.Score).Take(limit).ToList();
    }

    public IReadOnlyList<DiagnosticSummary> ListDiagnostics(string? lang = null, string? filter = null)
    {
        string[] terms = filter is null ? [] : Words(filter);
        return Set(lang).Diagnostics
            .Where(d => terms.Length == 0 || Score($"{d.Code} {d.Title} {d.Page}", terms) > 0)
            .Select(d => new DiagnosticSummary(d.Code, d.Title, d.Severity, d.Page))
            .ToList();
    }

    public IReadOnlyList<DiagnosticEntry> GetDiagnostic(string code, string? lang = null) =>
        Set(lang).Diagnostics.Where(d => d.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<ExampleSummary> ListExamples(string? style = null, string? tag = null)
    {
        string? wanted = NormalizeStyle(style);
        return data.Examples
            .Where(e => (wanted is null || e.Style == wanted)
                        && (string.IsNullOrEmpty(tag) || e.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            .Select(Summarize)
            .ToList();
    }

    public ExampleEntry? GetExample(string id) => data.Examples.FirstOrDefault(e => e.Id == id);

    public IReadOnlyList<ExampleSummary> SearchExamples(string query, string? style = null, int limit = 10)
    {
        string[] terms = Words(query);
        string? wanted = NormalizeStyle(style);
        return data.Examples
            .Where(e => wanted is null || e.Style == wanted)
            .Select(e => (Example: e, Score: Score($"{string.Join(' ', e.Title)} {string.Join(' ', e.Description)} {string.Join(' ', e.Tags)} {e.Id}", terms) * 5 + Score(e.Code, terms)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => Summarize(x.Example))
            .ToList();
    }

    private static ExampleSummary Summarize(ExampleEntry e) => new(e.Id, e.Style, e.Title[0], e.Description[0], e.Tags);

    [GeneratedRegex(@"[^\p{L}\p{N}_.#-]+")]
    private static partial Regex WordSplit();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("```\\w*[^\\n]*style=(?<style>\\w+)[^\\n]*\\n[\\s\\S]*?```\\n?")]
    private static partial Regex CodeFence();
}
