using Microsoft.CodeAnalysis;

namespace YotsubaShader.Roslyn.Model;

public readonly record struct SymbolKey(string Assembly, string DocId)
{
    public static SymbolKey Of(ISymbol symbol)
    {
        var definition = symbol.OriginalDefinition;

        var id = DocumentationCommentId.CreateDeclarationId(definition)
                 ?? throw new NotSupportedException(
                     $"'{symbol.ToDisplayString()}' no tiene una identidad estable.");

        return new SymbolKey(definition.ContainingAssembly.Name, id);
    }
    
    public static SymbolKey OfType(ITypeSymbol type) =>
        new(type.ContainingAssembly?.Name ?? "",
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
}