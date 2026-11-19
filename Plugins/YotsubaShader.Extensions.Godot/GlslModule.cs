using System.Text;
using System.Text.RegularExpressions;

namespace YotsubaShader.Extensions.Godot;

/// <summary>Variable <c>in</c>/<c>out</c> de la interfaz de una etapa.</summary>
internal sealed record GlslInterface(string Type, string Name, int Location, bool Flat);

/// <summary>Miembro de un bloque uniform: <c>type name[array];</c>.</summary>
internal sealed record GlslMember(string Type, string Name, string Array);

internal sealed record GlslBlock(string Qualifier, string BlockName, string? Instance, IReadOnlyList<GlslMember> Members);

internal sealed record GlslResource(string Type, string Name);

internal sealed record GlslFunction(string Name, string Text);

/// <summary>
/// La salida de SPIRV-Cross (GLSL 450 con semántica de Vulkan) partida en sus declaraciones de primer nivel.
/// El traductor de Godot trabaja sobre estas piezas, no sobre líneas sueltas.
/// </summary>
internal sealed class GlslModule
{
    public List<GlslInterface> Inputs { get; } = new();
    public List<GlslInterface> Outputs { get; } = new();
    public List<GlslBlock> Blocks { get; } = new();
    public List<GlslResource> Resources { get; } = new();
    public List<GlslFunction> Structs { get; } = new();
    public List<GlslFunction> Globals { get; } = new();
    public List<GlslFunction> Functions { get; } = new();
    public string MainBody { get; private set; } = "";

    private static readonly Regex Interface = new(
        @"^layout\s*\(([^)]*)\)\s*((?:(?:flat|noperspective|smooth|centroid)\s+)*)(in|out)\s+(\w+)\s+(\w+)\s*(\[\s*\d+\s*\])?\s*;$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex Block = new(
        @"^(?:layout\s*\([^)]*\)\s*)?(uniform|buffer)\s+(\w+)\s*\{(.*)\}\s*(\w+)?\s*(?:\[[^\]]*\])?\s*;$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex Resource = new(
        @"^(?:layout\s*\([^)]*\)\s*)?(?:(?:readonly|writeonly|coherent|restrict|volatile)\s+)*uniform\s+(\w+)\s+(\w+)\s*;$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FunctionHead = new(
        @"^[\w\[\]]+\s+(\w+)\s*\(", RegexOptions.Compiled);

    private static readonly Regex Layout = new(@"layout\s*\([^)]*\)", RegexOptions.Compiled);

    public static GlslModule Parse(string glsl)
    {
        var module = new GlslModule();
        foreach (string raw in Split(glsl))
        {
            string chunk = raw.Trim();
            if (chunk.Length == 0 || chunk[0] == '#') continue;

            Match match;
            if ((match = Interface.Match(chunk)).Success)
            {
                Match location = Regex.Match(match.Groups[1].Value, @"location\s*=\s*(\d+)");
                var variable = new GlslInterface(match.Groups[4].Value, match.Groups[5].Value,
                    location.Success ? int.Parse(location.Groups[1].Value) : -1,
                    match.Groups[2].Value.Contains("flat"));
                if (match.Groups[6].Success)
                {
                    throw new NotSupportedException(
                        $"'{variable.Name}': los arrays en la interfaz de una etapa no tienen equivalente en .gdshader.");
                }

                (match.Groups[3].Value == "in" ? module.Inputs : module.Outputs).Add(variable);
            }
            else if ((match = Block.Match(chunk)).Success)
            {
                module.Blocks.Add(new GlslBlock(match.Groups[1].Value, match.Groups[2].Value,
                    match.Groups[4].Success ? match.Groups[4].Value : null, ParseMembers(match.Groups[3].Value)));
            }
            else if ((match = Resource.Match(chunk)).Success)
            {
                module.Resources.Add(new GlslResource(match.Groups[1].Value, match.Groups[2].Value));
            }
            else if (chunk.StartsWith("struct ", StringComparison.Ordinal))
            {
                module.Structs.Add(new GlslFunction(Regex.Match(chunk, @"^struct\s+(\w+)").Groups[1].Value, chunk));
            }
            else if (chunk.Contains('{') && FunctionHead.IsMatch(chunk))
            {
                string name = FunctionHead.Match(chunk).Groups[1].Value;
                if (name == "main")
                {
                    int open = chunk.IndexOf('{');
                    int close = chunk.LastIndexOf('}');
                    module.MainBody = chunk.Substring(open + 1, close - open - 1);
                }
                else
                {
                    module.Functions.Add(new GlslFunction(name, chunk));
                }
            }
            else
            {
                string name = Regex.Match(chunk, @"(\w+)\s*(?:\[[^\]]*\])?\s*(?:=|;)").Groups[1].Value;
                module.Globals.Add(new GlslFunction(name, chunk));
            }
        }

        return module;
    }

    private static List<GlslMember> ParseMembers(string body)
    {
        var members = new List<GlslMember>();
        foreach (string part in body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string text = Layout.Replace(part, "");
            text = Regex.Replace(text, @"\b(row_major|column_major|highp|mediump|lowp|flat|restrict|readonly|writeonly|coherent|volatile)\b", "");
            Match match = Regex.Match(text.Trim(), @"^(\w+)\s+(\w+)\s*((?:\[[^\]]*\])*)$");
            if (!match.Success)
            {
                throw new NotSupportedException($"No se puede interpretar el miembro de buffer '{part}'.");
            }

            members.Add(new GlslMember(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
        }

        return members;
    }

    /// <summary>Parte el texto en declaraciones de primer nivel (hasta ';' o hasta el '}' de una función).</summary>
    private static IEnumerable<string> Split(string source)
    {
        var current = new StringBuilder();
        int braces = 0, parens = 0;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            bool atTop = braces == 0 && parens == 0;

            if (atTop && current.Length == 0 || atTop && IsBlank(current))
            {
                if (c == '#' || c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    int end = source.IndexOf('\n', i);
                    if (end < 0) end = source.Length;
                    string line = source.Substring(i, end - i);
                    i = end;
                    current.Clear();
                    if (c == '#') yield return line;
                    continue;
                }
            }

            current.Append(c);
            switch (c)
            {
                case '(': parens++; break;
                case ')': parens--; break;
                case '{': braces++; break;
                case '}':
                    braces--;
                    if (braces == 0)
                    {
                        string text = current.ToString();
                        string head = text.Substring(0, text.IndexOf('{'));
                        if (Layout.Replace(head, "").Contains('('))
                        {
                            yield return text;
                            current.Clear();
                        }
                    }

                    break;
                case ';':
                    if (braces == 0 && parens == 0)
                    {
                        yield return current.ToString();
                        current.Clear();
                    }

                    break;
            }
        }

        if (!IsBlank(current)) yield return current.ToString();
    }

    private static bool IsBlank(StringBuilder text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i])) return false;
        }

        return true;
    }
}
