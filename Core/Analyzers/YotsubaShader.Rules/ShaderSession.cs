using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace YotsubaShader.Rules;

/// <summary>
/// Estado compartido de un análisis completo: los hechos de cada método y el grafo de llamadas. Las reglas que
/// cruzan funciones (recursión, operaciones de una etapa usadas desde otra) se evalúan al final con
/// <see cref="Complete"/>, desde cada punto de entrada.
/// </summary>
internal sealed class ShaderSession
{
    private readonly ConcurrentDictionary<IMethodSymbol, MethodFacts> _facts = new(SymbolEqualityComparer.Default);
    private readonly ConcurrentDictionary<IMethodSymbol, bool> _published = new(SymbolEqualityComparer.Default);

    public ShaderSession(ShaderTargets targets) => Targets = targets;

    public ShaderTargets Targets { get; }

    public MethodFacts FactsFor(IMethodSymbol method) => _facts.GetOrAdd(method.OriginalDefinition, m => new MethodFacts(m));

    public bool HasFacts(IMethodSymbol method) => _facts.ContainsKey(method.OriginalDefinition);

    /// <summary>Analiza el cuerpo de un método (y de sus funciones locales).</summary>
    public void AnalyzeBody(IMethodSymbol method, IOperation body) =>
        BodyRules.Analyze(method.OriginalDefinition, body, Targets, FactsFor);

    /// <summary>Publica ya los diagnósticos del cuerpo de un método (y de sus funciones locales).</summary>
    public void Publish(IMethodSymbol method, Action<Diagnostic> report)
    {
        if (!_published.TryAdd(method.OriginalDefinition, true)) return;
        if (_facts.TryGetValue(method.OriginalDefinition, out MethodFacts? facts))
        {
            foreach (Diagnostic diagnostic in facts.Diagnostics) report(diagnostic);
            foreach (var call in facts.Calls.Where(c => c.Callee.MethodKind == MethodKind.LocalFunction))
            {
                Publish(call.Callee, report);
            }
        }
    }

    public static bool IsInShaderType(IMethodSymbol method)
    {
        for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.ContainingType)
        {
            if (ShaderSymbols.IsShaderType(type)) return true;
        }

        return false;
    }

    /// <summary>Reglas entre funciones, desde cada punto de entrada de <paramref name="shaderTypes"/>.</summary>
    public void Complete(IEnumerable<INamedTypeSymbol> shaderTypes, Action<Diagnostic> report)
    {
        var seen = new HashSet<(string, int, int)>();
        void Once(Diagnostic diagnostic)
        {
            var span = diagnostic.Location.SourceSpan;
            if (seen.Add((diagnostic.Id, span.Start, diagnostic.Location.SourceTree?.GetHashCode() ?? 0))) report(diagnostic);
        }

        foreach (INamedTypeSymbol type in shaderTypes)
        {
            foreach (IMethodSymbol entry in ShaderSymbols.EntryPoints(type))
            {
                var stages = ShaderSymbols.GetStages(entry);
                if (stages.Count == 0) continue;
                ShaderStageKind stage = stages[0].Stage;

                foreach (IMethodSymbol method in Reachable(entry))
                {
                    if (!_facts.TryGetValue(method, out MethodFacts? facts)) continue;
                    Publish(method, Once);

                    foreach (StageUsage usage in facts.StageUsages)
                    {
                        if (usage.Requirement == StageRequirement.FragmentOnly && stage != ShaderStageKind.Fragment)
                        {
                            Once(Diagnostic.Create(ShaderRules.FragmentOnly, usage.Location, usage.Name, usage.Detail,
                                DeclarationRules.StageName(stage), entry.Name));
                        }
                        else if (usage.Requirement == StageRequirement.ComputeOnly && stage != ShaderStageKind.Compute)
                        {
                            Once(Diagnostic.Create(ShaderRules.ComputeOnly, usage.Location, usage.Name,
                                DeclarationRules.StageName(stage), entry.Name));
                        }
                    }
                }

                ReportRecursion(entry, Once);
            }
        }
    }

    private IEnumerable<IMethodSymbol> Reachable(IMethodSymbol entry)
    {
        var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<IMethodSymbol>();
        queue.Enqueue(entry.OriginalDefinition);
        while (queue.Count > 0)
        {
            IMethodSymbol method = queue.Dequeue();
            if (!visited.Add(method)) continue;
            yield return method;
            if (_facts.TryGetValue(method, out MethodFacts? facts))
            {
                foreach (var call in facts.Calls) queue.Enqueue(call.Callee);
            }
        }
    }

    private void ReportRecursion(IMethodSymbol entry, Action<Diagnostic> report)
    {
        var path = new List<IMethodSymbol>();
        var onPath = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var done = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

        void Visit(IMethodSymbol method)
        {
            if (done.Contains(method)) return;
            path.Add(method);
            onPath.Add(method);

            if (_facts.TryGetValue(method, out MethodFacts? facts))
            {
                foreach (var call in facts.Calls)
                {
                    if (onPath.Contains(call.Callee))
                    {
                        int start = path.FindIndex(m => SymbolEqualityComparer.Default.Equals(m, call.Callee));
                        string cycle = string.Join(" → ", path.Skip(start).Select(m => m.Name).Concat(new[] { call.Callee.Name }));
                        report(Diagnostic.Create(ShaderRules.Recursion, call.Location, call.Callee.Name, cycle));
                    }
                    else
                    {
                        Visit(call.Callee);
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            onPath.Remove(method);
            done.Add(method);
        }

        Visit(entry.OriginalDefinition);
    }
}

/// <summary>
/// Validación completa de una compilación, para el compilador de shaders. Usa exactamente las mismas reglas que
/// el analizador del IDE: si el IDE no marca errores, aquí tampoco los hay.
/// </summary>
internal static class ShaderValidator
{
    public static IReadOnlyList<Diagnostic> Validate(Compilation compilation, ShaderTargets targets, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<Diagnostic>();
        var session = new ShaderSession(targets);
        var shaderTypes = AllTypes(compilation.Assembly.GlobalNamespace).Where(ShaderSymbols.IsShaderType).ToList();

        foreach (INamedTypeSymbol type in AllTypes(compilation.Assembly.GlobalNamespace))
        {
            VectorRules.AnalyzeType(type, diagnostics.Add);
        }

        foreach (INamedTypeSymbol type in shaderTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeclarationRules.AnalyzeShaderType(type, targets, diagnostics.Add);

            var queue = new Queue<IMethodSymbol>(ShaderSymbols.EntryPoints(type));
            var analyzed = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            while (queue.Count > 0)
            {
                IMethodSymbol method = queue.Dequeue().OriginalDefinition;
                if (!analyzed.Add(method)) continue;

                if (method.MethodKind != MethodKind.LocalFunction && !session.HasFacts(method))
                {
                    foreach (SyntaxReference reference in method.DeclaringSyntaxReferences)
                    {
                        SyntaxNode node = reference.GetSyntax(cancellationToken);
                        IOperation? operation = compilation.GetSemanticModel(node.SyntaxTree).GetOperation(node, cancellationToken);
                        if (operation is not null) session.AnalyzeBody(method, operation);
                    }
                }

                foreach (var call in session.FactsFor(method).Calls) queue.Enqueue(call.Callee);
            }
        }

        session.Complete(shaderTypes, diagnostics.Add);
        return diagnostics;
    }

    public static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol ns)
    {
        foreach (INamespaceOrTypeSymbol member in ns.GetMembers())
        {
            if (member is INamespaceSymbol child)
            {
                foreach (INamedTypeSymbol type in AllTypes(child)) yield return type;
            }
            else if (member is INamedTypeSymbol type)
            {
                foreach (INamedTypeSymbol nested in WithNested(type)) yield return nested;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> WithNested(INamedTypeSymbol type)
    {
        yield return type;
        foreach (INamedTypeSymbol nested in type.GetTypeMembers())
        {
            foreach (INamedTypeSymbol inner in WithNested(nested)) yield return inner;
        }
    }
}
