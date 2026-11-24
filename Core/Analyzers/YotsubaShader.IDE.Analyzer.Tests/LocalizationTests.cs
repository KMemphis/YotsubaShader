using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using YotsubaShader.Rules;
using static YotsubaShader.IDE.Analyzer.Tests.AnalyzerHarness;

namespace YotsubaShader.IDE.Analyzer.Tests;

/// <summary>Los diagnósticos están escritos en inglés y se traducen al español cuando la interfaz está en español.</summary>
public class LocalizationTests
{
    private const string Double = "public class S { [ShaderStage(Stage.Compute)] [numthreads(1,1,1)] public void CS() { double d = 1.0; } }";

    private static readonly CultureInfo Spanish = new("es-ES");
    private static readonly CultureInfo English = new("en-US");

    private static string Placeholders(string text) =>
        string.Join(",", Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal));

    private static string Message(string code, string id, string? targets, CultureInfo culture)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture;
            return Expect(code, id, targets).GetMessage();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Message_is_english_by_default()
    {
        string message = Message(Double, "YS0701", "Target.WebGpu", English);

        Assert.Contains("is not available in all graphics APIs", message);
        Assert.DoesNotContain("no está disponible", message);
    }

    [Fact]
    public void Message_is_spanish_when_the_interface_language_is_spanish()
    {
        string message = Message(Double, "YS0701", "Target.WebGpu", Spanish);

        Assert.Contains("no está disponible en todas las APIs gráficas", message);
        Assert.Contains("no existe en WGSL", message);
        Assert.DoesNotContain("is not available", message);
    }

    [Fact]
    public void Other_languages_fall_back_to_english()
    {
        Assert.Contains("is not available in all graphics APIs", Message(Double, "YS0701", "Target.WebGpu", new CultureInfo("fr-FR")));
        Assert.Contains("is not available in all graphics APIs", Message(Double, "YS0701", "Target.WebGpu", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void The_culture_given_by_the_compiler_wins_over_the_process_culture()
    {
        Diagnostic diagnostic = Expect(Double, "YS0701", "Target.WebGpu");

        Assert.Contains("no está disponible", diagnostic.GetMessage(Spanish));
        Assert.Contains("is not available", diagnostic.GetMessage(English));
    }

    [Fact]
    public void Every_rule_has_a_spanish_translation_with_the_same_placeholders()
    {
        foreach (DiagnosticDescriptor rule in ShaderRules.All)
        {
            string title = rule.Title.ToString(English);
            string format = rule.MessageFormat.ToString(English);

            Assert.True(Loc.Es.TryGetValue(title, out string? spanishTitle), $"{rule.Id}: missing translation of the title '{title}'");
            Assert.True(Loc.Es.TryGetValue(format, out string? spanishFormat), $"{rule.Id}: missing translation of the message '{format}'");
            Assert.Equal(Placeholders(format), Placeholders(spanishFormat!));
            Assert.Equal(spanishTitle, rule.Title.ToString(Spanish));
            Assert.Equal(spanishFormat, rule.MessageFormat.ToString(Spanish));
        }
    }

    [Fact]
    public void Translations_keep_the_placeholders_of_the_english_text()
    {
        foreach ((string english, string spanish) in Loc.Es)
        {
            Assert.True(Placeholders(english) == Placeholders(spanish), $"Placeholders differ: '{english}' / '{spanish}'");
        }
    }

    [Fact]
    public void Every_text_in_the_code_is_english_and_has_a_translation()
    {
        string root = SourceRoot();
        var literals = new List<(string File, string Text)>();
        var patterns = new[]
        {
            @"Loc\.(?:T|F)\(\s*(?:[^""()]*\?\s*)?""((?:[^""\\]|\\.)*)""(?:\s*:\s*""((?:[^""\\]|\\.)*)"")?",
            @"portabilityNote:\s*""((?:[^""\\]|\\.)*)""",
            @"\.Feature\(ApiFeature\.\w+,\s*Availability\.\w+,\s*""((?:[^""\\]|\\.)*)""",
            @"Limit\([^;]*,\s*""((?:[^""\\]|\\.)*)""\)"
        };

        foreach (string file in new[] { "YotsubaShader.Rules", "YotsubaShader.IDE.Analyzer" }
                     .SelectMany(project => Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
                     .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                                    !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                                    !path.EndsWith("Loc.Es.cs", StringComparison.Ordinal) && !path.EndsWith("Loc.cs", StringComparison.Ordinal)))
        {
            string source = File.ReadAllText(file);
            foreach (string pattern in patterns)
            {
                foreach (Match match in Regex.Matches(source, pattern))
                {
                    foreach (Group group in match.Groups.Cast<Group>().Skip(1).Where(group => group.Success))
                    {
                        literals.Add((Path.GetFileName(file), Regex.Unescape(group.Value)));
                    }
                }
            }
        }

        Assert.True(literals.Count > 100, "The source scan found too few texts: " + literals.Count);
        foreach ((string file, string text) in literals)
        {
            Assert.True(text.All(c => c < 128 || c is '≤' or '–' or '→'), $"{file}: '{text}' is not in English");
            Assert.True(Loc.Es.ContainsKey(text), $"{file}: no Spanish translation for '{text}'");
        }
    }

    private static string SourceRoot()
    {
        for (string? dir = AppContext.BaseDirectory; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "YotsubaShader.Rules");
            if (Directory.Exists(candidate)) return Path.GetDirectoryName(candidate)!;
        }

        throw new DirectoryNotFoundException("YotsubaShader.Rules was not found above " + AppContext.BaseDirectory);
    }
}
