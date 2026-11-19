using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using YotsubaShader.Roslyn.Model;

namespace YotsubaShader.Roslyn;

/// <summary>
/// Funciones locales: se compilan como funciones auxiliares. Las variables que capturan del método que las
/// contiene se pasan como parámetros extra por referencia (o <c>in</c> si solo se leen), así la función local
/// ve y modifica la variable original, igual que en C#.
/// </summary>
public partial class StatementOperationProcessor
{
    internal static ImmutableArray<CaptureInfo> CapturesOf(IMethodSymbol localFunction) =>
        [.. CollectCaptures(localFunction, new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default))];

    private static List<CaptureInfo> CollectCaptures(IMethodSymbol localFunction, HashSet<IMethodSymbol> visiting)
    {
        var captures = new List<CaptureInfo>();
        if (localFunction.IsStatic || !visiting.Add(localFunction)) return captures;

        SyntaxReference? reference = localFunction.DeclaringSyntaxReferences.FirstOrDefault();
        if (reference is null) return captures;

        SyntaxNode syntax = reference.GetSyntax();
        if (ShaderProcessor.Sources.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax) is not ILocalFunctionOperation operation)
        {
            return captures;
        }

        void Add(ISymbol symbol, ITypeSymbol type, bool written)
        {
            int index = captures.FindIndex(c => SymbolEqualityComparer.Default.Equals(c.Symbol, symbol));
            if (index < 0) captures.Add(new CaptureInfo(symbol, type, written));
            else if (written && !captures[index].Written) captures[index] = captures[index] with { Written = true };
        }

        foreach (IOperation descendant in operation.Descendants())
        {
            switch (descendant)
            {
                case ILocalReferenceOperation local when !IsDeclaredInside(local.Local, localFunction):
                    Add(local.Local, local.Local.Type, IsWritten(local));
                    break;
                case IParameterReferenceOperation parameter when !IsDeclaredInside(parameter.Parameter, localFunction):
                    Add(parameter.Parameter, parameter.Parameter.Type, IsWritten(parameter));
                    break;
                case IInvocationOperation { TargetMethod.MethodKind: MethodKind.LocalFunction } call:
                    // Lo que captura una función local anidada también tiene que llegar hasta aquí.
                    foreach (CaptureInfo nested in CollectCaptures(call.TargetMethod.OriginalDefinition, visiting))
                    {
                        if (!IsDeclaredInside(nested.Symbol, localFunction)) Add(nested.Symbol, nested.Type, nested.Written);
                    }

                    break;
            }
        }

        visiting.Remove(localFunction);
        return captures;
    }

    /// <summary>¿El símbolo pertenece a la función local (o a algo declarado dentro de ella)?</summary>
    private static bool IsDeclaredInside(ISymbol symbol, IMethodSymbol localFunction)
    {
        for (ISymbol? owner = symbol.ContainingSymbol; owner is not null; owner = owner.ContainingSymbol)
        {
            if (SymbolEqualityComparer.Default.Equals(owner, localFunction)) return true;
            if (owner is IMethodSymbol { MethodKind: not (MethodKind.LocalFunction or MethodKind.AnonymousFunction) }) return false;
        }

        return false;
    }

    private static bool IsWritten(IOperation reference)
    {
        IOperation current = reference;
        while (current.Parent is IFieldReferenceOperation or IPropertyReferenceOperation or IArrayElementReferenceOperation)
        {
            current = current.Parent;
        }

        return current.Parent switch
        {
            ISimpleAssignmentOperation assignment => assignment.Target == current,
            ICompoundAssignmentOperation compound => compound.Target == current,
            IIncrementOrDecrementOperation => true,
            IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } => true,
            IDeconstructionAssignmentOperation => true,
            _ => false
        };
    }
}
