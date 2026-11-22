#region MS-PL License - Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic
/*
╔════════════════════════════════════════════════════════════════════════════════════╗
║                                                                                    ║
║  ███╗   ███╗███████╗      ██████╗ ██╗                                              ║
║  ████╗ ████║██╔════╝      ██╔══██╗██║                                              ║
║  ██╔████╔██║███████╗█████╗██████╔╝██║                                              ║
║  ██║╚██╔╝██║╚════██║╚════╝██╔═══╝ ██║                                              ║
║  ██║ ╚═╝ ██║███████║      ██║     ███████╗                                         ║
║  ╚═╝     ╚═╝╚══════╝      ╚═╝     ╚══════╝                                         ║
║                                                                                    ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║  Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic                 ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  Microsoft Public License (MS-PL)                                                  ║
║                                                                                    ║
║  This license governs use of the accompanying software. If you use the software,   ║
║  you accept this license. If you do not accept the license, do not use the         ║
║  software.                                                                         ║
║                                                                                    ║
║  1. Definitions                                                                    ║
║  The terms "reproduce," "reproduction," "derivative works," and "distribution"     ║
║  have the same meaning here as under U.S. copyright law.                           ║
║  A "contribution" is the original software, to which copyright notices are         ║
║  attached, and any additions or changes to the software.                           ║
║  A "contributor" is any person that distributes its contribution under this        ║
║  license.                                                                          ║
║  "Licensed patents" are a contributor's patent claims that read directly on its    ║
║  contribution.                                                                     ║
║                                                                                    ║
║  2. Grant of Rights                                                                ║
║  (A) Copyright Grant- Subject to the terms of this license, including the license  ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free copyright license to reproduce its             ║
║  contribution, prepare derivative works of its contribution, and distribute its    ║
║  contribution or any derivative works that you create.                             ║
║  (B) Patent Grant- Subject to the terms of this license, including the license     ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free license under its licensed patents to make,    ║
║  have made, use, sell, offer for sale, import, and/or otherwise dispose of its     ║
║  contribution in the software or derivative works of the contribution in the       ║
║  software.                                                                         ║
║                                                                                    ║
║  3. Conditions and Limitations                                                     ║
║  (A) No Trademark License- This license does not grant you rights to use any       ║
║  contributors' name, logo, or trademarks.                                          ║
║  (B) If you bring a patent claim against any contributor over patents that you     ║
║  claim are infringed by the software, your patent license from such contributor    ║
║  to the software ends automatically.                                               ║
║  (C) If you distribute any portion of the software, you must retain all copyright, ║
║  patent, trademark, and attribution notices that are present in the software.      ║
║  (D) If you distribute any portion of the software in source code form, you may do ║
║  so only under this license by including a complete copy of this license with      ║
║  your distribution. If you distribute any portion of the software in compiled or   ║
║  object code form, you may only do so under a license that complies with this      ║
║  license.                                                                          ║
║  (E) The software is licensed "as-is." You bear the risk of using it. The          ║
║  contributors give no express warranties, guarantees or conditions. You may have   ║
║  additional consumer rights under your local laws which this license cannot        ║
║  change. To the extent permitted under your local laws, the contributors exclude   ║
║  the implied warranties of merchantability, fitness for a particular purpose and   ║
║  non-infringement.                                                                 ║
║                                                                                    ║
╚════════════════════════════════════════════════════════════════════════════════════╝
*/
#endregion

using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace YotsubaShader.Compiler.CLI;

/// <summary>
/// Servidor de lenguaje mínimo (LSP sobre stdin/stdout) para editar shaders C# desde un editor. Abre el proyecto con
/// Roslyn, aplica el texto que el editor tiene sin guardar y publica los diagnósticos del compilador y de los
/// analizadores de YotsubaShader (<c>YS....</c>) cada vez que el documento cambia.
/// </summary>
internal sealed class LanguageServer
{
    private const int DebounceMilliseconds = 250;

    private readonly Stream _input = Console.OpenStandardInput();
    private readonly Stream _output = Console.OpenStandardOutput();
    private readonly object _writeLock = new();
    private readonly string _projectPath;
    private readonly Dictionary<string, string> _openTexts = new();
    private readonly HashSet<string> _published = new();
    private readonly object _stateLock = new();

    private MSBuildWorkspace? _workspace;
    private Project? _project;
    private CancellationTokenSource? _analysis;
    private bool _shutdown;

    private LanguageServer(string projectPath) => _projectPath = Path.GetFullPath(projectPath);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("Uso: yotsuba lsp <proyecto.csproj>");
            return 2;
        }

        // stdout es solo para el protocolo: cualquier Console.WriteLine accidental iría a stderr.
        Console.SetOut(Console.Error);

        var server = new LanguageServer(args[0]);
        try
        {
            return await server.LoopAsync();
        }
        finally
        {
            server._workspace?.Dispose();
        }
    }

    private async Task<int> LoopAsync()
    {
        while (true)
        {
            JsonNode? message = ReadMessage();
            if (message is null) return _shutdown ? 0 : 1;

            string? method = message["method"]?.GetValue<string>();
            JsonNode? id = message["id"]?.DeepClone();
            JsonNode? parameters = message["params"];

            switch (method)
            {
                case "initialize":
                    Respond(id, new JsonObject
                    {
                        ["capabilities"] = new JsonObject
                        {
                            ["textDocumentSync"] = new JsonObject
                            {
                                ["openClose"] = true,
                                ["change"] = 1, // Full: el documento entero en cada cambio
                            },
                            ["completionProvider"] = new JsonObject { ["triggerCharacters"] = new JsonArray(".") },
                            ["hoverProvider"] = true,
                            ["codeActionProvider"] = true,
                        },
                        ["serverInfo"] = new JsonObject { ["name"] = "yotsuba-lsp" },
                    });
                    break;

                case "initialized":
                    _ = Task.Run(LoadProjectAsync);
                    break;

                case "textDocument/didOpen":
                {
                    JsonNode document = parameters!["textDocument"]!;
                    lock (_stateLock) _openTexts[ToPath(document["uri"]!.GetValue<string>())] = document["text"]!.GetValue<string>();
                    ScheduleAnalysis();
                    break;
                }

                case "textDocument/didChange":
                {
                    string path = ToPath(parameters!["textDocument"]!["uri"]!.GetValue<string>());
                    JsonArray changes = parameters["contentChanges"]!.AsArray();
                    lock (_stateLock) _openTexts[path] = changes[^1]!["text"]!.GetValue<string>();
                    ScheduleAnalysis();
                    break;
                }

                case "textDocument/didClose":
                {
                    string uri = parameters!["textDocument"]!["uri"]!.GetValue<string>();
                    lock (_stateLock)
                    {
                        _openTexts.Remove(ToPath(uri));
                        _published.Remove(uri);
                    }

                    Publish(uri, new JsonArray());
                    break;
                }

                case "textDocument/completion":
                    RunRequest(id, () => CompleteAsync(parameters!));
                    break;

                case "textDocument/hover":
                    RunRequest(id, () => HoverAsync(parameters!));
                    break;

                case "yotsuba/classify":
                    RunRequest(id, () => ClassifyAsync(parameters!));
                    break;

                case "textDocument/codeAction":
                    RunRequest(id, () => CodeActionsAsync(parameters!));
                    break;

                case "shutdown":
                    _shutdown = true;
                    _analysis?.Cancel();
                    Respond(id, null);
                    break;

                case "exit":
                    return _shutdown ? 0 : 1;

                default:
                    // Una petición que no soportamos se responde con error; las notificaciones se ignoran.
                    if (id is not null) RespondError(id, -32601, $"Método no soportado: {method}");
                    break;
            }
        }
    }

    private async Task LoadProjectAsync()
    {
        try
        {
            if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();

            _workspace = MSBuildWorkspace.Create();
            _workspace.RegisterWorkspaceFailedHandler(e => Log($"MSBuild: {e.Diagnostic.Message}"));
            Project project = await _workspace.OpenProjectAsync(_projectPath);
            lock (_stateLock) _project = project;
            Log($"Proyecto cargado: {_projectPath}");
            ScheduleAnalysis();
        }
        catch (Exception exception)
        {
            Log($"No se pudo abrir el proyecto: {exception.Message}");
        }
    }

    private void ScheduleAnalysis()
    {
        CancellationTokenSource source = new();
        CancellationTokenSource? previous;
        lock (_stateLock)
        {
            previous = _analysis;
            _analysis = source;
        }

        previous?.Cancel();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceMilliseconds, source.Token);
                await AnalyzeAsync(source.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Log($"Análisis fallido: {exception}");
            }
        });
    }

    private async Task AnalyzeAsync(CancellationToken token)
    {
        Project? project;
        Dictionary<string, string> texts;
        lock (_stateLock)
        {
            project = _project;
            texts = new Dictionary<string, string>(_openTexts);
        }

        if (project is null || texts.Count == 0) return;

        Project current = WithOpenTexts(project, texts).GetProject(project.Id)!;
        Compilation compilation = await current.GetCompilationAsync(token)
            ?? throw new InvalidOperationException("No se pudo crear la compilación.");

        ImmutableArray<DiagnosticAnalyzer> analyzers = current.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(LanguageNames.CSharp))
            .ToImmutableArray();

        ImmutableArray<Diagnostic> diagnostics = analyzers.IsEmpty
            ? compilation.GetDiagnostics(token)
            : await compilation
                .WithAnalyzers(analyzers, new CompilationWithAnalyzersOptions(current.AnalyzerOptions, null, true, false))
                .GetAllDiagnosticsAsync(token);

        token.ThrowIfCancellationRequested();

        var byPath = diagnostics
            .Where(d => d.Severity != DiagnosticSeverity.Hidden && d.Location.IsInSource && !IsStyleNoise(d))
            .GroupBy(d => d.Location.GetLineSpan().Path)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (string path in texts.Keys)
        {
            var items = new JsonArray();
            if (byPath.TryGetValue(path, out List<Diagnostic>? list))
            {
                foreach (Diagnostic diagnostic in list) items.Add(ToLsp(diagnostic));
            }

            Publish(new Uri(path).AbsoluteUri, items);
        }
    }

    /// <summary>
    /// Las sugerencias de estilo de .NET (CA1822 «puede ser static»...) no valen para un shader: se muestran solo
    /// los errores del compilador y las reglas de YotsubaShader.
    /// </summary>
    private static bool IsStyleNoise(Diagnostic diagnostic) =>
        diagnostic.Id.StartsWith("CA", StringComparison.Ordinal) || diagnostic.Id.StartsWith("IDE", StringComparison.Ordinal);

    /// <summary>El texto sin guardar del editor sustituye al del disco, solo en esta copia de la solución.</summary>
    private static Solution WithOpenTexts(Project project, Dictionary<string, string> texts)
    {
        Solution solution = project.Solution;
        foreach ((string path, string text) in texts)
        {
            DocumentId? documentId = solution.GetDocumentIdsWithFilePath(path).FirstOrDefault();
            if (documentId is not null) solution = solution.WithDocumentText(documentId, SourceText.From(text));
            else if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                // Archivo creado desde el editor tras cargar el proyecto: se analiza igual, sin esperar a recargar.
                solution = solution.AddDocument(DocumentId.CreateNewId(project.Id), Path.GetFileName(path), SourceText.From(text), filePath: path);
        }

        return solution;
    }

    /// <summary>Documento actual (con el texto del editor) y la posición pedida por el cliente.</summary>
    private async Task<(Document Document, int Position)?> FindAsync(JsonNode parameters)
    {
        string path = ToPath(parameters["textDocument"]!["uri"]!.GetValue<string>());
        Project? project;
        Dictionary<string, string> texts;
        lock (_stateLock)
        {
            project = _project;
            texts = new Dictionary<string, string>(_openTexts);
        }

        if (project is null) return null;

        Solution solution = WithOpenTexts(project, texts);
        DocumentId? documentId = solution.GetDocumentIdsWithFilePath(path).FirstOrDefault();
        if (documentId is null) return null;

        Document document = solution.GetDocument(documentId)!;
        SourceText text = await document.GetTextAsync();
        JsonNode position = (parameters["position"] ?? parameters["range"]!["start"])!;
        int line = Math.Clamp(Num(position["line"]!), 0, text.Lines.Count - 1);
        int character = Math.Clamp(Num(position["character"]!), 0, text.Lines[line].Span.Length);
        return (document, text.Lines.GetPosition(new LinePosition(line, character)));
    }

    private async Task<JsonNode?> CompleteAsync(JsonNode parameters)
    {
        if (await FindAsync(parameters) is not var (document, position)) return null;

        CompletionService? service = CompletionService.GetService(document);
        if (service is null) return null;

        CompletionList list = await service.GetCompletionsAsync(document, position);

        // Roslyn devuelve todos los símbolos visibles; se filtran y ordenan por lo que ya se escribió (mejor primero).
        SourceText text = await document.GetTextAsync();
        int start = position;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) start--;
        string typed = text.ToString(TextSpan.FromBounds(start, position));
        IReadOnlyList<CompletionItem> ranked = typed.Length == 0
            ? list.ItemsList
            : service.FilterItems(document, list.ItemsList.ToImmutableArray(), typed);

        var items = new JsonArray();
        int withImport = 0;
        foreach (CompletionItem item in ranked.Take(200))
        {
            string insert = item.Properties.TryGetValue("InsertionText", out string? insertion) && !string.IsNullOrEmpty(insertion)
                ? insertion
                : item.DisplayText;
            string label = item.DisplayText;
            var edits = new JsonArray();

            // Lo que aún no está importado (InlineDescription = su espacio de nombres): se sugiere igual y, al aceptarlo,
            // se añade el «using». Roslyn calcula el cambio completo (el texto y la línea del using, en su sitio).
            if (!string.IsNullOrEmpty(item.InlineDescription) && withImport++ < 30)
            {
                CompletionChange change = await service.GetChangeAsync(document, item);
                foreach (TextChange textChange in change.TextChanges)
                {
                    if (textChange.Span.End <= start)
                    {
                        LinePositionSpan span = text.Lines.GetLinePositionSpan(textChange.Span);
                        edits.Add(new JsonObject
                        {
                            ["range"] = new JsonObject { ["start"] = Position(span.Start), ["end"] = Position(span.End) },
                            ["newText"] = textChange.NewText,
                        });
                    }
                    else if (textChange.NewText is { Length: > 0 } replacement)
                    {
                        insert = replacement;
                    }
                }

                if (edits.Count > 0) label = $"{item.DisplayText}  (using {item.InlineDescription})";
            }

            var json = new JsonObject
            {
                ["label"] = label,
                ["kind"] = CompletionKind(item.Tags),
                ["sortText"] = edits.Count > 0 ? "~" + item.SortText : item.SortText,
                ["filterText"] = item.FilterText,
                ["insertText"] = insert,
                ["detail"] = item.InlineDescription,
            };
            if (edits.Count > 0) json["additionalTextEdits"] = edits;
            items.Add(json);
        }

        // Dentro de [ ]: Roslyn solo ofrece atributos sin importar si su nombre acaba en «Attribute». Los de YotsubaShader
        // (BuiltIn_Position, Location...) no, así que se añaden aquí, con su «using».
        if (typed.Length >= 1)
        {
            Compilation? compilation = await document.Project.GetCompilationAsync();
            SyntaxNode? root = await document.GetSyntaxRootAsync();
            bool attribute = root?.FindToken(Math.Max(0, position - 1)).Parent?.AncestorsAndSelf().Any(n => n is AttributeListSyntax) == true;
            if (compilation is not null && root is CompilationUnitSyntax unit && attribute)
            {
                var present = items.Select(item => item!["filterText"]?.GetValue<string>() ?? "").ToHashSet();
                var imported = unit.Usings.Select(u => u.Name?.ToString() ?? "").ToHashSet();
                int added = 0;
                foreach (TypeEntry entry in (await FindTypesAsync(compilation, entry => entry.IsAttribute))
                    .OrderBy(entry => entry.Name, StringComparer.Ordinal))
                {
                    string display = entry.Name.EndsWith("Attribute", StringComparison.Ordinal) && entry.Name.Length > 9
                        ? entry.Name[..^"Attribute".Length]
                        : entry.Name;
                    if (!display.StartsWith(typed, StringComparison.OrdinalIgnoreCase) || imported.Contains(entry.Namespace)) continue;
                    if (present.Contains(display)) continue;
                    if (added++ >= 40) break;

                    items.Add(new JsonObject
                    {
                        ["label"] = $"{display}  (using {entry.Namespace})",
                        ["kind"] = entry.Kind,
                        ["sortText"] = "~" + display,
                        ["filterText"] = display,
                        ["insertText"] = display,
                        ["detail"] = $"using {entry.Namespace};",
                        ["additionalTextEdits"] = new JsonArray(UsingEdit(unit, text, entry.Namespace)),
                    });
                }
            }
        }

        return new JsonObject { ["isIncomplete"] = false, ["items"] = items };
    }

    /// <summary>Acciones rápidas: para un nombre sin resolver (CS0246, CS0103...) ofrece «using» de los tipos que lo declaran.</summary>
    private async Task<JsonNode?> CodeActionsAsync(JsonNode parameters)
    {
        if (await FindAsync(parameters) is not var (document, _)) return null;

        Compilation? compilation = await document.Project.GetCompilationAsync();
        SyntaxNode? root = await document.GetSyntaxRootAsync();
        if (compilation is null || root is not CompilationUnitSyntax unit) return null;

        SourceText text = await document.GetTextAsync();
        string uri = parameters["textDocument"]!["uri"]!.GetValue<string>();
        var imported = unit.Usings.Select(u => u.Name?.ToString() ?? "").ToHashSet();
        var actions = new JsonArray();
        var offered = new HashSet<string>();

        foreach (JsonNode? diagnostic in parameters["context"]?["diagnostics"]?.AsArray() ?? new JsonArray())
        {
            string? code = diagnostic?["code"]?.GetValue<string>();
            if (code is not ("CS0246" or "CS0103" or "CS0234" or "CS0305")) continue;

            JsonNode range = diagnostic!["range"]!;
            int line = Math.Clamp(Num(range["start"]!["line"]!), 0, text.Lines.Count - 1);
            int column = Math.Min(Num(range["start"]!["character"]!), text.Lines[line].Span.Length);
            int begin = text.Lines[line].Start + column;
            int end = begin;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
            string name = text.ToString(TextSpan.FromBounds(begin, end));
            if (name.Length == 0) continue;

            bool attribute = root.FindToken(begin).Parent?.AncestorsAndSelf().Any(n => n is AttributeListSyntax) == true;
            foreach (TypeEntry entry in await FindTypesAsync(compilation, entry =>
                         entry.Name == name || (attribute && entry.IsAttribute && entry.Name == name + "Attribute")))
            {
                if (imported.Contains(entry.Namespace) || !offered.Add(entry.Namespace + "|" + name)) continue;
                if (offered.Count > 12) break;

                actions.Add(new JsonObject
                {
                    ["title"] = $"using {entry.Namespace};",
                    ["kind"] = "quickfix",
                    ["diagnostics"] = new JsonArray(diagnostic.DeepClone()),
                    ["edit"] = new JsonObject
                    {
                        ["changes"] = new JsonObject { [uri] = new JsonArray(UsingEdit(unit, text, entry.Namespace)) },
                    },
                });
            }
        }

        return actions;
    }

    /// <summary>El «using» se inserta en orden: los de System primero, luego alfabético, como hace Visual Studio.</summary>
    private static JsonObject UsingEdit(CompilationUnitSyntax unit, SourceText text, string ns)
    {
        static int Order(string name) => name == "System" || name.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1;

        int line;
        UsingDirectiveSyntax[] usings = unit.Usings.Where(u => u.Alias is null && u.StaticKeyword.IsKind(SyntaxKind.None)).ToArray();
        if (usings.Length == 0)
        {
            line = unit.Members.Count > 0 ? text.Lines.GetLineFromPosition(unit.Members[0].FullSpan.Start).LineNumber : 0;
        }
        else
        {
            UsingDirectiveSyntax? next = usings.FirstOrDefault(u =>
            {
                string name = u.Name?.ToString() ?? "";
                int byGroup = Order(ns).CompareTo(Order(name));
                return byGroup < 0 || (byGroup == 0 && string.CompareOrdinal(ns, name) < 0);
            });
            line = next is not null
                ? text.Lines.GetLineFromPosition(next.SpanStart).LineNumber
                : text.Lines.GetLineFromPosition(usings[^1].Span.End).LineNumber + 1;
        }

        return new JsonObject
        {
            ["range"] = new JsonObject { ["start"] = Position(new LinePosition(line, 0)), ["end"] = Position(new LinePosition(line, 0)) },
            ["newText"] = $"using {ns};\n",
        };
    }

    // ── Índice de tipos de las referencias del proyecto (para sugerir lo que aún no está importado) ──

    private sealed record TypeEntry(string Name, string Namespace, int Kind, bool IsAttribute);

    private Task<List<TypeEntry>>? _referenceTypes;

    /// <summary>Los tipos de las referencias no cambian al editar: se indexan una vez. Los del propio proyecto, cada vez.</summary>
    private async Task<IEnumerable<TypeEntry>> FindTypesAsync(Compilation compilation, Func<TypeEntry, bool> filter)
    {
        _referenceTypes ??= Task.Run(() =>
        {
            var found = new List<TypeEntry>();
            var seen = new HashSet<string>();
            foreach (MetadataReference reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    Walk(assembly.GlobalNamespace, publicOnly: true, found, seen);
                }
            }

            return found;
        });

        var own = new List<TypeEntry>();
        Walk(compilation.Assembly.GlobalNamespace, publicOnly: false, own, new HashSet<string>());
        return (await _referenceTypes).Concat(own).Where(filter).ToList();
    }

    private static void Walk(INamespaceSymbol ns, bool publicOnly, List<TypeEntry> into, HashSet<string> seen)
    {
        if (!ns.IsGlobalNamespace)
        {
            string name = ns.ToDisplayString();
            foreach (INamedTypeSymbol type in ns.GetTypeMembers())
            {
                bool visible = type.DeclaredAccessibility == Accessibility.Public ||
                               (!publicOnly && type.DeclaredAccessibility == Accessibility.Internal);
                if (!visible || type.IsImplicitlyDeclared || type.Name.Contains('<') || !seen.Add(name + "." + type.Name)) continue;

                int kind = type.TypeKind switch
                {
                    TypeKind.Interface => 8,
                    TypeKind.Enum => 13,
                    TypeKind.Struct => 22,
                    _ => 7,
                };
                into.Add(new TypeEntry(type.Name, name, kind, IsAttributeType(type)));
            }
        }

        foreach (INamespaceSymbol child in ns.GetNamespaceMembers()) Walk(child, publicOnly, into, seen);
    }

    private static bool IsAttributeType(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract) return false;
        for (INamedTypeSymbol? baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.Name == "Attribute" && baseType.ContainingNamespace.ToDisplayString() == "System") return true;
        }

        return false;
    }

    private async Task<JsonNode?> HoverAsync(JsonNode parameters)
    {
        if (await FindAsync(parameters) is not var (document, position)) return null;

        QuickInfoService? service = QuickInfoService.GetService(document);
        QuickInfoItem? info = service is null ? null : await service.GetQuickInfoAsync(document, position);
        if (info is null) return null;

        string text = string.Join("\n", info.Sections.Select(section => section.Text).Where(t => t.Length > 0));
        return new JsonObject { ["contents"] = new JsonObject { ["kind"] = "plaintext", ["value"] = text } };
    }

    /// <summary>
    /// Coloreado semántico: Roslyn clasifica cada identificador por lo que es (clase, método, parámetro...), no por cómo se
    /// escribe. Devuelve [línea, columna inicial, columna final, tipo] solo para los identificadores; palabras clave, cadenas,
    /// comentarios y números los sigue pintando el resaltador léxico del cliente.
    /// </summary>
    private async Task<JsonNode?> ClassifyAsync(JsonNode parameters)
    {
        string path = ToPath(parameters["textDocument"]!["uri"]!.GetValue<string>());
        Project? project;
        Dictionary<string, string> texts;
        lock (_stateLock)
        {
            project = _project;
            texts = new Dictionary<string, string>(_openTexts);
        }

        if (project is null) return null;
        Solution solution = WithOpenTexts(project, texts);
        DocumentId? documentId = solution.GetDocumentIdsWithFilePath(path).FirstOrDefault();
        if (documentId is null) return null;

        Document document = solution.GetDocument(documentId)!;
        SourceText source = await document.GetTextAsync();
        var spans = await Microsoft.CodeAnalysis.Classification.Classifier.GetClassifiedSpansAsync(document, new Microsoft.CodeAnalysis.Text.TextSpan(0, source.Length));
        var result = new JsonArray();
        foreach (var span in spans)
        {
            string kind = span.ClassificationType;
            if (kind.EndsWith("name", StringComparison.Ordinal) is false || kind.StartsWith("preprocessor", StringComparison.Ordinal)
                || kind.StartsWith("xml", StringComparison.Ordinal)) continue;
            LinePositionSpan lines = source.Lines.GetLinePositionSpan(span.TextSpan);
            if (lines.Start.Line != lines.End.Line) continue;
            result.Add(new JsonArray(lines.Start.Line, lines.Start.Character, lines.End.Character, kind));
        }

        return result;
    }

    /// <summary>Etiquetas de Roslyn (WellKnownTags) → CompletionItemKind del protocolo.</summary>
    private static int CompletionKind(ImmutableArray<string> tags)
    {
        foreach (string tag in tags)
        {
            switch (tag)
            {
                case "Method" or "ExtensionMethod": return 2;
                case "Field" or "Constant": return 5;
                case "Local" or "Parameter" or "RangeVariable": return 6;
                case "Class": return 7;
                case "Interface": return 8;
                case "Namespace" or "Module": return 9;
                case "Property": return 10;
                case "Enum": return 13;
                case "Keyword": return 14;
                case "Structure": return 22;
            }
        }

        return 1;
    }

    /// <summary>Atiende una petición en segundo plano: el bucle de mensajes no espera a Roslyn.</summary>
    private void RunRequest(JsonNode? id, Func<Task<JsonNode?>> work) => _ = Task.Run(async () =>
    {
        try
        {
            Respond(id, await work());
        }
        catch (Exception exception)
        {
            Log($"Petición fallida: {exception.Message}");
            Respond(id, null);
        }
    });

    private static JsonObject ToLsp(Diagnostic diagnostic)
    {
        FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
        return new JsonObject
        {
            ["range"] = new JsonObject
            {
                ["start"] = Position(span.StartLinePosition),
                ["end"] = Position(span.EndLinePosition),
            },
            ["severity"] = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => 1,
                DiagnosticSeverity.Warning => 2,
                _ => 3,
            },
            ["code"] = diagnostic.Id,
            ["source"] = diagnostic.Id.StartsWith("YS", StringComparison.Ordinal) ? "yotsuba" : "csharp",
            ["message"] = diagnostic.GetMessage(),
        };
    }

    /// <summary>Godot serializa los números como 13.0: se aceptan enteros y decimales.</summary>
    private static int Num(JsonNode? node) => (int)Math.Round(node!.GetValue<double>());

    private static JsonObject Position(LinePosition position) =>
        new() { ["line"] = position.Line, ["character"] = position.Character };

    private void Publish(string uri, JsonArray diagnostics) =>
        Notify("textDocument/publishDiagnostics", new JsonObject { ["uri"] = uri, ["diagnostics"] = diagnostics });

    private static string ToPath(string uri) => new Uri(uri).LocalPath;

    private static void Log(string text) => Console.Error.WriteLine($"[yotsuba-lsp] {text}");

    // ── Transporte: cabeceras "Content-Length" + cuerpo JSON ──

    private JsonNode? ReadMessage()
    {
        int length = -1;
        while (true)
        {
            string? line = ReadHeaderLine();
            if (line is null) return null;
            if (line.Length == 0) break;
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                length = int.Parse(line["Content-Length:".Length..].Trim());
            }
        }

        if (length < 0) return null;

        byte[] buffer = new byte[length];
        int read = 0;
        while (read < length)
        {
            int count = _input.Read(buffer, read, length - read);
            if (count == 0) return null;
            read += count;
        }

        return JsonNode.Parse(buffer);
    }

    private string? ReadHeaderLine()
    {
        var bytes = new List<byte>();
        while (true)
        {
            int value = _input.ReadByte();
            if (value < 0) return null;
            if (value == '\n') break;
            if (value != '\r') bytes.Add((byte)value);
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private void Send(JsonObject message)
    {
        message["jsonrpc"] = "2.0";
        byte[] body = Encoding.UTF8.GetBytes(message.ToJsonString());
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        lock (_writeLock)
        {
            _output.Write(header);
            _output.Write(body);
            _output.Flush();
        }
    }

    private void Respond(JsonNode? id, JsonNode? result) =>
        Send(new JsonObject { ["id"] = id, ["result"] = result });

    private void RespondError(JsonNode? id, int code, string message) =>
        Send(new JsonObject { ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } });

    private void Notify(string method, JsonNode parameters) =>
        Send(new JsonObject { ["method"] = method, ["params"] = parameters });
}
