using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using YotsubaShader.Rules;

namespace YotsubaShader.IDE.Analyzer;

/// <summary>
/// Analizador de YotsubaShader para el IDE. Aplica las mismas reglas que el compilador de shaders
/// (<c>YotsubaShader.Rules</c>), así que un shader sin errores aquí compila para la GPU.
/// <list type="bullet">
/// <item>Declaraciones (entry points, interfaz, recursos) y cuerpos de los métodos del shader: en cuanto se escriben.</item>
/// <item>Funciones auxiliares de otras clases, recursión y operaciones de una etapa usadas desde otra: al final de la
/// compilación, desde cada punto de entrada.</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ShaderAnalyzer : BaseAnalyser
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ShaderRules.All.ToImmutableArray();

    protected override void Register(AnalysisContext context)
    {
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(ShaderNames.ShaderStage) is null &&
                start.Compilation.GetTypeByMetadataName(ShaderNames.Vector) is null)
            {
                return;
            }

            var session = new ShaderSession();
            var shaderTypes = new ConcurrentBag<INamedTypeSymbol>();

            start.RegisterSymbolAction(symbolContext =>
            {
                var type = (INamedTypeSymbol)symbolContext.Symbol;
                VectorRules.AnalyzeType(type, symbolContext.ReportDiagnostic);

                if (ShaderSymbols.IsShaderType(type))
                {
                    shaderTypes.Add(type);
                    DeclarationRules.AnalyzeShaderType(type, TargetResolver.For(type), symbolContext.ReportDiagnostic);
                }
            }, SymbolKind.NamedType);

            start.RegisterOperationBlockAction(blockContext =>
            {
                if (blockContext.OwningSymbol is not IMethodSymbol method) return;

                foreach (IOperation block in blockContext.OperationBlocks)
                {
                    session.AnalyzeBody(method, block);
                }

                // El código de una clase shader siempre es código de GPU: se avisa en cuanto se escribe.
                if (ShaderSession.IsInShaderType(method))
                {
                    session.Publish(method, blockContext.ReportDiagnostic);
                }
            });

            start.RegisterCompilationEndAction(endContext =>
            {
                session.Complete(shaderTypes, endContext.ReportDiagnostic);
            });
        });
    }
}
