using System.Text;
using System.Text.RegularExpressions;
using ShadowDusk.Core;

namespace YotsubaShader.Compiler.Library.CompileToGameEngines;

/// <summary>
/// SPIRV-Cross emite un programa HLSL completo por stage, y ambos repiten nombres
/// (<c>SPIRV_Cross_Input/Output</c>, structs del usuario, variables estáticas). Para poder
/// incluir los dos en un mismo .fx, los structs de interfaz se renombran por entry point y los
/// bloques de primer nivel idénticos a los del vértice se quitan del fragmento.
/// </summary>
internal static partial class HlslMerge
{
    /// <summary>Perfiles de vértice y fragmento que usa el technique para cada target de ShadowDusk.</summary>
    public static (string Vs, string Ps) ProfilesFor(PlatformTarget target) => target switch
    {
        PlatformTarget.DirectX => ("vs_4_0", "ps_4_0"),
        PlatformTarget.DirectX12 or PlatformTarget.Vulkan => ("vs_6_0", "ps_6_0"),
        _ => ("vs_3_0", "ps_3_0"),
    };

    public static (string Vertex, string Fragment) Separate(
        string vertexHlsl, string vertexEntry,
        string fragmentHlsl, string fragmentEntry)
    {
        string vertex = Rename(vertexHlsl, vertexEntry);
        string fragment = Rename(fragmentHlsl, fragmentEntry);

        var seen = new HashSet<string>(SplitTopLevelBlocks(vertex).Select(block => block.Trim()));
        var kept = SplitTopLevelBlocks(fragment).Where(block => !seen.Contains(block.Trim()));

        return (vertex, string.Join("\n\n", kept) + "\n");
    }

    private static string Rename(string hlsl, string entry) =>
        InterfaceStructRegex().Replace(hlsl, match => $"SPIRV_Cross_{entry}_{match.Groups[1].Value}");

    /// <summary>
    /// Separa el texto en declaraciones de primer nivel: cada línea <c>...;</c> suelta, y cada
    /// struct o función completa (hasta que se cierran sus llaves).
    /// </summary>
    private static IEnumerable<string> SplitTopLevelBlocks(string hlsl)
    {
        var block = new StringBuilder();
        int depth = 0;

        foreach (string line in hlsl.ReplaceLineEndings("\n").Split('\n'))
        {
            if (depth == 0 && block.Length == 0 && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            block.AppendLine(line);
            depth += line.Count(c => c == '{') - line.Count(c => c == '}');

            string trimmed = line.TrimEnd();

            if (depth == 0 && (trimmed.EndsWith(';') || trimmed.EndsWith('}')))
            {
                yield return block.ToString().TrimEnd();
                block.Clear();
            }
        }

        if (block.Length > 0)
        {
            yield return block.ToString().TrimEnd();
        }
    }

    [GeneratedRegex(@"\bSPIRV_Cross_(Input|Output)\b")]
    private static partial Regex InterfaceStructRegex();
}
