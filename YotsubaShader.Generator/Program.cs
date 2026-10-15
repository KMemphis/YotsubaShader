#region MIT License - Copyright (c) 2026 Memphis Yomael
/*
╔════════════════════════════════════════════════════════════════════════════════════╗
║                                                                                    ║
║  ███╗   ███╗██╗████████╗    ██╗     ██╗ ██████╗███████╗███╗   ██╗███████╗███████╗  ║
║  ████╗ ████║██║╚══██╔══╝    ██║     ██║██╔════╝██╔════╝████╗  ██║██╔════╝██╔════╝  ║
║  ██╔████╔██║██║   ██║       ██║     ██║██║     █████╗  ██╔██╗ ██║███████╗█████╗    ║
║  ██║╚██╔╝██║██║   ██║       ██║     ██║██║     ██╔══╝  ██║╚██╗██║╚════██║██╔══╝    ║
║  ██║ ╚═╝ ██║██║   ██║       ███████╗██║╚██████╗███████╗██║ ╚████║███████║███████╗  ║
║  ╚═╝     ╚═╝╚═╝   ╚═╝       ╚══════╝╚═╝ ╚═════╝╚══════╝╚═╝  ╚═══╝╚══════╝╚══════╝  ║
║                                                                                    ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║  Copyright (c) 2026 Memphis Yomael                                                 ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  Permission is hereby granted, free of charge, to any person obtaining a copy      ║
║  of this software and associated documentation files (the "Software"), to deal     ║
║  in the Software without restriction, including without limitation the rights      ║
║  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell         ║
║  copies of the Software, and to permit persons to whom the Software is             ║
║  furnished to do so, subject to the following conditions:                          ║
║                                                                                    ║
║  The above copyright notice and this permission notice shall be included           ║
║  in all copies or substantial portions of the Software.                            ║
║                                                                                    ║
║  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR        ║
║  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,          ║
║  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE       ║
║  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER            ║
║  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,     ║
║  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE     ║
║  SOFTWARE.                                                                         ║
║                                                                                    ║
╚════════════════════════════════════════════════════════════════════════════════════╝
*/
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace YotsubaShader.Generator;

/// <summary>
/// Genera <c>SpirVOpTable</c>, <c>SpirVOp</c> y <c>SpirVOpClass</c> a partir de la gramática
/// oficial de Khronos (<c>spirv.core.grammar.json</c>), que el proyecto consumidor declara
/// como <c>AdditionalFiles</c>.
/// </summary>
// Nombre completo: dentro del namespace YotsubaShader.Compiler.SpirV.Generator, "Generator" podría confundirse con él.
[global::Microsoft.CodeAnalysis.Generator(LanguageNames.CSharp)]
public sealed class SpirVOpTableGenerator : IIncrementalGenerator
{
    private const string GrammarFileName = "spirv.core.grammar.json";
    private const string TableNamespace = "YotsubaShader.Compiler.SpirV";
    private const string TypesNamespace = "YotsubaShader.Compiler.SpirV.YS_Types";

    private static readonly DiagnosticDescriptor MissingGrammar = new(
        "YSG0001", "Falta la gramática SPIR-V",
        "No se encontró '" + GrammarFileName + "' entre los AdditionalFiles; SpirVOpTable no se generará",
        "YotsubaShader.Compiler.SpirV.Generator", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidGrammar = new(
        "YSG0002", "Gramática SPIR-V inválida",
        "No se pudo procesar '{0}': {1}",
        "YotsubaShader.Compiler.SpirV.Generator", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateGrammar = new(
        "YSG0003", "Gramática SPIR-V duplicada",
        "Hay {0} archivos '" + GrammarFileName + "' en AdditionalFiles; deja solo uno",
        "YotsubaShader.Compiler.SpirV.Generator", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1) Filtrar el archivo por nombre. 2) Parsear y generar el texto (el resultado es
        //    comparable por valor, así que si el JSON no cambia no se regenera nada).
        IncrementalValuesProvider<GenerationResult> results = context.AdditionalTextsProvider
            .Where(static file => string.Equals(Path.GetFileName(file.Path), GrammarFileName,
                                                StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => GenerationResult.From(file, ct));

        context.RegisterSourceOutput(results.Collect(), static (spc, all) =>
        {
            if (all.Length == 0)
            {
                spc.ReportDiagnostic(Diagnostic.Create(MissingGrammar, Location.None));
                return;
            }
            if (all.Length > 1)
            {
                spc.ReportDiagnostic(Diagnostic.Create(DuplicateGrammar, Location.None, all.Length));
                return;
            }

            GenerationResult r = all[0];
            if (r.Error is not null)
            {
                Location where = Location.Create(r.Path, default(TextSpan), default(LinePositionSpan));
                spc.ReportDiagnostic(Diagnostic.Create(InvalidGrammar, where, r.Path, r.Error));
                return;
            }

            spc.AddSource("SpirVOp.g.cs", SourceText.From(r.EnumSource!, Encoding.UTF8));
            spc.AddSource("SpirVOpTable.g.cs", SourceText.From(r.TableSource!, Encoding.UTF8));
        });
    }

    // ------------------------------------------------------------------------------------
    // Resultado del paso Select: solo cadenas, para que la comparación por valor funcione.
    // ------------------------------------------------------------------------------------
    private sealed class GenerationResult : IEquatable<GenerationResult>
    {
        public string Path { get; }
        public string? Error { get; }
        public string? EnumSource { get; }
        public string? TableSource { get; }

        private GenerationResult(string path, string? error, string? enumSource, string? tableSource)
        {
            Path = path;
            Error = error;
            EnumSource = enumSource;
            TableSource = tableSource;
        }

        public static GenerationResult From(AdditionalText file, CancellationToken ct)
        {
            try
            {
                SourceText? text = file.GetText(ct);
                if (text is null)
                    return new GenerationResult(file.Path, "no se pudo leer el archivo", null, null);

                Grammar grammar = Grammar.Parse(text.ToString());
                ct.ThrowIfCancellationRequested();
                return new GenerationResult(file.Path, null,
                                            Emitter.EmitEnums(grammar), Emitter.EmitTable(grammar));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new GenerationResult(file.Path, ex.Message, null, null);
            }
        }

        public bool Equals(GenerationResult? other) =>
            other is not null && Path == other.Path && Error == other.Error &&
            EnumSource == other.EnumSource && TableSource == other.TableSource;

        public override bool Equals(object? obj) => Equals(obj as GenerationResult);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Path.GetHashCode();
                h = h * 31 + (Error?.GetHashCode() ?? 0);
                h = h * 31 + (TableSource?.Length ?? 0);
                return h;
            }
        }
    }

    // ------------------------------------------------------------------------------------
    // Modelo de la gramática (solo lo que necesitamos)
    // ------------------------------------------------------------------------------------
    private sealed class Instruction
    {
        public string Name = "";
        public int Opcode;
        public string Class = "";
        public bool HasResultType;
        public bool HasResult;
        public int MinWordCount;
        public bool IsVariableLength;
        public uint Version;          // 0x00MMmm00, 0 = solo mediante extensión/capacidad
        public string? LastVersion;
        public bool Provisional;
        public List<string> Aliases = new();
        public List<string> Capabilities = new();
        public List<string> Extensions = new();
        public List<string> OperandDescriptions = new();
    }

    private sealed class PrintingClass
    {
        public string Tag = "";
        public string? Heading;
    }

    private sealed class Grammar
    {
        public uint MagicNumber;
        public int Major, Minor, Revision;
        public List<PrintingClass> Classes = new();
        public List<Instruction> Instructions = new();

        public static Grammar Parse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object?>
                       ?? throw new FormatException("la raíz del JSON no es un objeto");

            var g = new Grammar
            {
                MagicNumber = ParseHex(GetString(root, "magic_number")),
                Major = GetInt(root, "major_version"),
                Minor = GetInt(root, "minor_version"),
                Revision = GetInt(root, "revision"),
            };

            foreach (var o in GetArray(root, "instruction_printing_class").OfType<Dictionary<string, object?>>())
                g.Classes.Add(new PrintingClass
                {
                    Tag = GetString(o, "tag"),
                    Heading = o.TryGetValue("heading", out var hd) ? hd as string : null,
                });

            // Tipos de operando que no ocupan exactamente una palabra.
            var parameterizedKinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kind in GetArray(root, "operand_kinds").OfType<Dictionary<string, object?>>())
            {
                if (!kind.TryGetValue("enumerants", out var en) || en is not List<object?> enumerants) continue;
                if (enumerants.OfType<Dictionary<string, object?>>().Any(e => e.ContainsKey("parameters")))
                    parameterizedKinds.Add(GetString(kind, "kind"));
            }

            var byOpcode = new Dictionary<int, Instruction>();
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var o in GetArray(root, "instructions").OfType<Dictionary<string, object?>>())
            {
                var ins = new Instruction
                {
                    Name = GetString(o, "opname"),
                    Opcode = GetInt(o, "opcode"),
                    Class = GetString(o, "class"),
                    Version = ParseVersion(o.TryGetValue("version", out var v) ? v as string : null),
                    LastVersion = o.TryGetValue("lastVersion", out var lv) ? lv as string : null,
                    Provisional = o.TryGetValue("provisional", out var pv) && pv is true,
                };

                if (ins.Opcode < 0 || ins.Opcode > ushort.MaxValue)
                    throw new FormatException($"{ins.Name}: opcode {ins.Opcode} fuera de rango (16 bits)");
                if (!IsIdentifier(ins.Name))
                    throw new FormatException($"'{ins.Name}' no es un identificador C# válido");

                ins.Capabilities.AddRange(GetStrings(o, "capabilities"));
                ins.Extensions.AddRange(GetStrings(o, "extensions"));
                foreach (string alias in GetStrings(o, "aliases"))
                    if (IsIdentifier(alias)) ins.Aliases.Add(alias);

                int words = 1; // la palabra de cabecera (Word Count | Opcode)
                bool variable = false;
                if (o.TryGetValue("operands", out var ops) && ops is List<object?> operands)
                {
                    foreach (var op in operands.OfType<Dictionary<string, object?>>())
                    {
                        string kind = GetString(op, "kind");
                        string? quantifier = op.TryGetValue("quantifier", out var q) ? q as string : null;
                        string? name = op.TryGetValue("name", out var n) ? n as string : null;

                        if (kind == "IdResultType") ins.HasResultType = true;
                        if (kind == "IdResult") ins.HasResult = true;

                        if (quantifier is not null) variable = true;
                        else words += kind.StartsWith("Pair", StringComparison.Ordinal) ? 2 : 1;

                        if (kind == "LiteralString" || kind == "LiteralContextDependentNumber" ||
                            parameterizedKinds.Contains(kind))
                            variable = true;

                        ins.OperandDescriptions.Add(kind + (name is null ? "" : " " + name) + (quantifier ?? ""));
                    }
                }
                ins.MinWordCount = words;
                ins.IsVariableLength = variable;

                // Gramáticas antiguas repiten el opcode con otro nombre en lugar de usar "aliases".
                if (byOpcode.TryGetValue(ins.Opcode, out Instruction? primary))
                {
                    if (names.Add(ins.Name)) primary.Aliases.Add(ins.Name);
                    continue;
                }

                if (!names.Add(ins.Name))
                    throw new FormatException($"el nombre '{ins.Name}' aparece dos veces");
                byOpcode.Add(ins.Opcode, ins);
                g.Instructions.Add(ins);
            }

            // Los alias no pueden chocar con nombres principales.
            foreach (var ins in g.Instructions)
                ins.Aliases.RemoveAll(a => g.Instructions.Any(p => p.Name == a && p != ins));

            if (g.Instructions.Count == 0)
                throw new FormatException("la gramática no contiene instrucciones");
            return g;
        }

        private static uint ParseVersion(string? text)
        {
            if (text is null || text == "None") return 0;
            string[] parts = text.Split('.');
            if (parts.Length != 2) throw new FormatException($"versión '{text}' no reconocida");
            int major = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int minor = int.Parse(parts[1], CultureInfo.InvariantCulture);
            return (uint)((major << 16) | (minor << 8)); // mismo formato que la palabra 1 del módulo
        }

        private static uint ParseHex(string text)
        {
            string digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.Substring(2) : text;
            return uint.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }

    // ------------------------------------------------------------------------------------
    // Emisión del código C#
    // ------------------------------------------------------------------------------------
    private static class Emitter
    {
        private const string Header =
            "// <auto-generated/>\n" +
            "// Generado por YotsubaShader.Compiler.SpirV.Generator a partir de spirv.core.grammar.json (Khronos, licencia MIT).\n" +
            "// No edites este archivo: modifica el JSON o el generador.\n" +
            "#nullable enable\n";

        public static string EmitEnums(Grammar g)
        {
            var sb = new StringBuilder(Header);
            sb.Append("\nnamespace ").Append(TypesNamespace).Append(";\n\n");

            sb.Append("/// <summary>Opcodes de SPIR-V ").Append(g.Major).Append('.').Append(g.Minor)
              .Append(" (revisión ").Append(g.Revision).Append(").</summary>\n");
            sb.Append("public enum SpirVOp : ushort\n{\n");
            foreach (var ins in g.Instructions.OrderBy(i => i.Opcode))
            {
                sb.Append("    /// <summary><c>").Append(ins.Name).Append("</c> · ").Append(Escape(ins.Class))
                  .Append("</summary>\n");
                sb.Append("    ").Append(ins.Name).Append(" = ").Append(ins.Opcode).Append(",\n");
                foreach (string alias in ins.Aliases)
                    sb.Append("    /// <summary>Alias de <see cref=\"").Append(ins.Name).Append("\"/>.</summary>\n")
                      .Append("    ").Append(alias).Append(" = ").Append(ins.Opcode).Append(",\n");
            }
            sb.Append("}\n\n");

            sb.Append("/// <summary>Clases de instrucción de la especificación (sección 3.x).</summary>\n");
            sb.Append("public enum SpirVOpClass : byte\n{\n");
            foreach (string tag in AllClassTags(g))
            {
                string? heading = g.Classes.FirstOrDefault(c => c.Tag == tag)?.Heading;
                sb.Append("    /// <summary>").Append(Escape(heading ?? tag)).Append("</summary>\n");
                sb.Append("    ").Append(ClassIdentifier(tag)).Append(",\n");
            }
            sb.Append("}\n");
            return sb.ToString();
        }

        public static string EmitTable(Grammar g)
        {
            var sb = new StringBuilder(Header);
            sb.Append("\nusing global::System;\n");
            sb.Append("using global::System.Collections.Generic;\n");
            sb.Append("using global::").Append(TypesNamespace).Append(";\n\n");
            sb.Append("namespace ").Append(TableNamespace).Append(";\n\n");

            sb.Append("/// <summary>Tabla de instrucciones SPIR-V ").Append(g.Major).Append('.').Append(g.Minor)
              .Append(" (revisión ").Append(g.Revision).Append("), generada desde la gramática oficial.</summary>\n");
            sb.Append("public static partial class SpirVOpTable\n{\n");
            sb.Append("    /// <summary>Palabra 0 de todo módulo SPIR-V.</summary>\n");
            sb.Append("    public const uint MagicNumber = 0x").Append(g.MagicNumber.ToString("X8", CultureInfo.InvariantCulture)).Append("u;\n");
            sb.Append("    public const int GrammarMajorVersion = ").Append(g.Major).Append(";\n");
            sb.Append("    public const int GrammarMinorVersion = ").Append(g.Minor).Append(";\n");
            sb.Append("    public const int GrammarRevision = ").Append(g.Revision).Append(";\n");
            sb.Append("    /// <summary>Versión de la gramática en el formato de la palabra 1 del módulo.</summary>\n");
            sb.Append("    public const uint GrammarVersionWord = 0x")
              .Append(((uint)((g.Major << 16) | (g.Minor << 8))).ToString("X8", CultureInfo.InvariantCulture)).Append("u;\n");

            // Campos agrupados por clase, en el orden de la especificación.
            foreach (string tag in AllClassTags(g))
            {
                var group = g.Instructions.Where(i => i.Class == tag).OrderBy(i => i.Opcode).ToList();
                if (group.Count == 0) continue;
                sb.Append("\n    #region ").Append(ClassIdentifier(tag)).Append("\n");
                foreach (var ins in group) EmitField(sb, ins);
                sb.Append("    #endregion\n");
            }

            var withAliases = g.Instructions.Where(i => i.Aliases.Count > 0).ToList();
            if (withAliases.Count > 0)
            {
                sb.Append("\n    #region Alias\n");
                foreach (var ins in withAliases)
                    foreach (string alias in ins.Aliases)
                        sb.Append("    /// <summary>Alias de <see cref=\"").Append(ins.Name).Append("\"/>.</summary>\n")
                          .Append("    public static readonly SpirVOperation ").Append(alias).Append(" = ")
                          .Append(ins.Name).Append(";\n");
                sb.Append("    #endregion\n");
            }

            // Colección completa (después de los campos: los inicializadores estáticos van en orden textual).
            sb.Append("\n    private static readonly SpirVOperation[] s_all =\n    {\n");
            foreach (var ins in g.Instructions.OrderBy(i => i.Opcode))
                sb.Append("        ").Append(ins.Name).Append(",\n");
            sb.Append("    };\n\n");
            sb.Append("    /// <summary>Todas las instrucciones (sin alias), ordenadas por opcode.</summary>\n");
            sb.Append("    public static IReadOnlyList<SpirVOperation> All => s_all;\n\n");

            // Búsqueda por opcode: un switch sin reservas de memoria.
            sb.Append("    /// <summary>Busca una instrucción por opcode.</summary>\n");
            sb.Append("    public static bool TryGet(SpirVOp opcode, out SpirVOperation operation)\n    {\n");
            sb.Append("        switch (opcode)\n        {\n");
            foreach (var ins in g.Instructions.OrderBy(i => i.Opcode))
                sb.Append("            case SpirVOp.").Append(ins.Name).Append(": operation = ").Append(ins.Name)
                  .Append("; return true;\n");
            sb.Append("            default: operation = default; return false;\n        }\n    }\n\n");

            sb.Append("    /// <summary>Devuelve la instrucción de un opcode o lanza si no existe.</summary>\n");
            sb.Append("    public static SpirVOperation Get(SpirVOp opcode) =>\n");
            sb.Append("        TryGet(opcode, out SpirVOperation op) ? op\n");
            sb.Append("            : throw new ArgumentOutOfRangeException(nameof(opcode), opcode, \"Opcode SPIR-V desconocido\");\n\n");

            // Búsqueda por nombre (incluye alias). El diccionario se crea la primera vez.
            sb.Append("    private static Dictionary<string, SpirVOperation>? s_byName;\n\n");
            sb.Append("    /// <summary>Busca una instrucción por nombre (\"OpFAdd\"); acepta alias.</summary>\n");
            sb.Append("    public static bool TryGet(string name, out SpirVOperation operation)\n    {\n");
            sb.Append("        s_byName ??= BuildNameIndex();\n");
            sb.Append("        return s_byName.TryGetValue(name, out operation);\n    }\n\n");
            sb.Append("    private static Dictionary<string, SpirVOperation> BuildNameIndex()\n    {\n");
            sb.Append("        var d = new Dictionary<string, SpirVOperation>(s_all.Length + ")
              .Append(g.Instructions.Sum(i => i.Aliases.Count)).Append(", StringComparer.Ordinal);\n");
            sb.Append("        foreach (SpirVOperation op in s_all) d[op.Name] = op;\n");
            foreach (var ins in withAliases)
                foreach (string alias in ins.Aliases)
                    sb.Append("        d[\"").Append(alias).Append("\"] = ").Append(ins.Name).Append(";\n");
            sb.Append("        return d;\n    }\n\n");

            EmitStringSets(sb, g, "RequiredCapabilities", "s_caps", i => i.Capabilities,
                "Capacidades que habilitan la instrucción (basta con declarar una). Vacío = siempre disponible.");
            EmitStringSets(sb, g, "RequiredExtensions", "s_exts", i => i.Extensions,
                "Extensiones que la introducen (<c>OpExtension</c>). Vacío = núcleo.");

            sb.Append("}\n");
            return sb.ToString();
        }

        private static void EmitField(StringBuilder sb, Instruction ins)
        {
            sb.Append("    /// <summary><c>").Append(ins.Name).Append("</c> = ").Append(ins.Opcode)
              .Append(" · ").Append(Escape(ins.Class))
              .Append(ins.Version == 0 ? " · solo con capacidad/extensión" : " · SPIR-V " + VersionText(ins.Version))
              .Append("</summary>\n");
            sb.Append("    /// <remarks>");
            sb.Append("Operandos: ").Append(ins.OperandDescriptions.Count == 0 ? "ninguno" : Escape(string.Join(", ", ins.OperandDescriptions))).Append('.');
            if (ins.Capabilities.Count > 0) sb.Append(" Capacidades: ").Append(Escape(string.Join(", ", ins.Capabilities))).Append('.');
            if (ins.Extensions.Count > 0) sb.Append(" Extensiones: ").Append(Escape(string.Join(", ", ins.Extensions))).Append('.');
            if (ins.LastVersion is not null) sb.Append(" Última versión: ").Append(Escape(ins.LastVersion)).Append('.');
            if (ins.Provisional) sb.Append(" Provisional.");
            sb.Append("</remarks>\n");

            sb.Append("    public static readonly SpirVOperation ").Append(ins.Name).Append(" = new(\"")
              .Append(ins.Name).Append("\", SpirVOp.").Append(ins.Name).Append(", SpirVOpClass.")
              .Append(ClassIdentifier(ins.Class)).Append(", ")
              .Append(ins.HasResultType ? "true" : "false").Append(", ")
              .Append(ins.HasResult ? "true" : "false").Append(", ")
              .Append(ins.MinWordCount).Append(", ")
              .Append(ins.IsVariableLength ? "true" : "false").Append(", 0x")
              .Append(ins.Version.ToString("X8", CultureInfo.InvariantCulture)).Append("u);\n");
        }

        private static void EmitStringSets(StringBuilder sb, Grammar g, string method, string prefix,
                                           Func<Instruction, List<string>> select, string summary)
        {
            // Un array por cada conjunto distinto, compartido entre instrucciones.
            var sets = new Dictionary<string, string>(StringComparer.Ordinal);
            var cases = new List<KeyValuePair<Instruction, string>>();
            foreach (var ins in g.Instructions.OrderBy(i => i.Opcode))
            {
                List<string> values = select(ins);
                if (values.Count == 0) continue;
                string key = string.Join("|", values);
                if (!sets.TryGetValue(key, out string? field))
                {
                    field = prefix + sets.Count.ToString(CultureInfo.InvariantCulture);
                    sets.Add(key, field);
                }
                cases.Add(new KeyValuePair<Instruction, string>(ins, field));
            }

            foreach (var pair in sets)
            {
                sb.Append("    private static readonly string[] ").Append(pair.Value).Append(" = { ");
                sb.Append(string.Join(", ", pair.Key.Split('|').Select(s => "\"" + s + "\""))).Append(" };\n");
            }
            sb.Append("\n    /// <summary>").Append(summary).Append("</summary>\n");
            sb.Append("    public static IReadOnlyList<string> ").Append(method).Append("(SpirVOp opcode) => opcode switch\n    {\n");
            foreach (var c in cases)
                sb.Append("        SpirVOp.").Append(c.Key.Name).Append(" => ").Append(c.Value).Append(",\n");
            sb.Append("        _ => Array.Empty<string>(),\n    };\n\n");
        }

        private static IEnumerable<string> AllClassTags(Grammar g)
        {
            // Primero las clases declaradas, en su orden; después cualquier clase nueva no declarada.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in g.Classes)
                if (seen.Add(c.Tag)) yield return c.Tag;
            foreach (var ins in g.Instructions)
                if (seen.Add(ins.Class)) yield return ins.Class;
        }

        private static string ClassIdentifier(string tag)
        {
            if (tag == "@exclude") return "Excluded";
            var sb = new StringBuilder();
            bool upper = true;
            foreach (char ch in tag)
            {
                if (!char.IsLetterOrDigit(ch)) { upper = true; continue; }
                sb.Append(upper ? char.ToUpperInvariant(ch) : ch);
                upper = false;
            }
            if (sb.Length == 0 || char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }

        private static string VersionText(uint v) =>
            ((v >> 16) & 0xFF).ToString(CultureInfo.InvariantCulture) + "." +
            ((v >> 8) & 0xFF).ToString(CultureInfo.InvariantCulture);

        // Texto seguro para un comentario XML de una sola línea (algunos nombres de operando
        // de la gramática contienen saltos de línea).
        private static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c)) { space = true; continue; }
                if (space && sb.Length > 0) sb.Append(' ');
                space = false;
                if (c == '&') sb.Append("&amp;");
                else if (c == '<') sb.Append("&lt;");
                else if (c == '>') sb.Append("&gt;");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------------------------
    private static bool IsIdentifier(string s) =>
        s.Length > 0 && (char.IsLetter(s[0]) || s[0] == '_') && s.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static string GetString(Dictionary<string, object?> o, string key) =>
        o.TryGetValue(key, out var v) && v is string s ? s
            : throw new FormatException($"falta la cadena '{key}'");

    private static int GetInt(Dictionary<string, object?> o, string key) =>
        o.TryGetValue(key, out var v) && v is long l ? checked((int)l)
            : throw new FormatException($"falta el entero '{key}'");

    private static List<object?> GetArray(Dictionary<string, object?> o, string key) =>
        o.TryGetValue(key, out var v) && v is List<object?> a ? a
            : throw new FormatException($"falta el array '{key}'");

    private static IEnumerable<string> GetStrings(Dictionary<string, object?> o, string key) =>
        o.TryGetValue(key, out var v) && v is List<object?> a ? a.OfType<string>() : Enumerable.Empty<string>();

    /// <summary>
    /// Lector JSON mínimo. Un generador se ejecuta dentro del compilador (netstandard2.0), donde
    /// System.Text.Json no está disponible sin empaquetar dependencias; para esta gramática basta esto.
    /// Objetos → Dictionary, arrays → List, números enteros → long, otros → double.
    /// </summary>
    private static class MiniJson
    {
        public static object? Parse(string s)
        {
            int i = 0;
            object? value = ReadValue(s, ref i);
            SkipWhitespace(s, ref i);
            if (i != s.Length) throw Error(s, i, "contenido extra tras el valor raíz");
            return value;
        }

        private static object? ReadValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw Error(s, i, "fin inesperado");
            char c = s[i];
            switch (c)
            {
                case '{': return ReadObject(s, ref i);
                case '[': return ReadArray(s, ref i);
                case '"': return ReadString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber(s, ref i);
                    throw Error(s, i, $"carácter inesperado '{c}'");
            }
        }

        private static Dictionary<string, object?> ReadObject(string s, ref int i)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            i++; // {
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw Error(s, i, "se esperaba el nombre de una propiedad");
                string key = ReadString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Error(s, i, "se esperaba ':'");
                i++;
                result[key] = ReadValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return result; }
                throw Error(s, i, "se esperaba ',' o '}'");
            }
        }

        private static List<object?> ReadArray(string s, ref int i)
        {
            var result = new List<object?>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }
            while (true)
            {
                result.Add(ReadValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return result; }
                throw Error(s, i, "se esperaba ',' o ']'");
            }
        }

        private static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // "
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Error(s, i, "escape \\u incompleto");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw Error(s, i, $"escape desconocido '\\{e}'");
                }
            }
            throw Error(s, i, "cadena sin cerrar");
        }

        private static object ReadNumber(string s, ref int i)
        {
            int start = i;
            bool isFloat = false;
            while (i < s.Length)
            {
                char c = s[i];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+') { i++; continue; }
                if (c == '.' || c == 'e' || c == 'E') { isFloat = true; i++; continue; }
                break;
            }
            string text = s.Substring(start, i - start);
            // Dos return separados: con "? :" el long se convertiría implícitamente a double.
            if (isFloat) return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            return long.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error(s, i, $"se esperaba '{word}'");
            i += word.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static FormatException Error(string s, int i, string message)
        {
            int line = 1;
            for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
            return new FormatException($"JSON, línea {line}: {message}");
        }
    }
}