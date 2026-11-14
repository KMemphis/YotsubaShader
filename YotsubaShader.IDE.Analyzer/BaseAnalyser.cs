using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YotsubaShader.IDE.Analyzer;

public abstract class BaseAnalyser : DiagnosticAnalyzer
{
    
    public abstract override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
    
    public sealed override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        Register(context);
    }
    
    protected abstract void Register(AnalysisContext context);
    
    protected static bool Has(ISymbol s, string metadataName)
    {
        foreach (var a in s.GetAttributes())
            if (a.AttributeClass?.ToDisplayString() == metadataName) return true;
        return false;
    }
}