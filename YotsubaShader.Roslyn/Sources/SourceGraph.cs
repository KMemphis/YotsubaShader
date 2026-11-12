using Microsoft.CodeAnalysis;

namespace YotsubaShader.Roslyn.Sources;

public sealed class SourceGraph
{
    private readonly IMethodSourceProvider[] _providers;
    private readonly Dictionary<SyntaxTree, Compilation> _owners = new Dictionary<SyntaxTree, Compilation>();

    public SourceGraph(Compilation user, params IMethodSourceProvider[] providers)
    {
        _providers = providers;
        Register(user);
    }

    public void Register(Compilation compilation)
    {
        foreach (var compilationSyntaxTree in compilation.SyntaxTrees)
        {
            _owners[compilationSyntaxTree] = compilation;
        }
    }

    public SemanticModel GetSemanticModel(SyntaxTree tree)
    {
        return _owners.TryGetValue(tree, out Compilation? owner)
            ? owner.GetSemanticModel(tree)
            : throw new InvalidOperationException("El árbol no pertenece a ninguna compilación registrada.");
    }

    public MethodSource Resolve(IMethodSymbol called)
    {
        if (!called.DeclaringSyntaxReferences.IsDefaultOrEmpty &&
            _owners.TryGetValue(called.DeclaringSyntaxReferences[0].SyntaxTree, out Compilation? owner))
        {
            return new MethodSource(called, owner);
        }
        
        foreach (var provider in _providers)
        {
            if (provider.TryGet(called) is { } source)
            {
                Register(source.Compilation);
                return source;
            }
        }
        
        throw new NotSupportedException(
            $"'{called.ToDisplayString()}' viene de '{called.ContainingAssembly.Name}' " +
            "y no hay código disponible para compilarlo en GPU.");
    }
}