using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ICSharpCode.Decompiler.Metadata;

namespace YotsubaShader.Roslyn.Sources;

public class DecompiledSourceProvider : IMethodSourceProvider
{
    private readonly Compilation _user;
    private readonly Dictionary<string, Compilation> _shadows = new Dictionary<string, Compilation>();

    public DecompiledSourceProvider(Compilation user) => _user = user;

    public MethodSource? TryGet(IMethodSymbol called)
    {
        IAssemblySymbol assembly = called.ContainingAssembly;
        
        if(_user.GetMetadataReference(assembly) is not PortableExecutableReference { FilePath: { } path })
        {
            return null;
        }

        if (FrameworkAssemblies.IsFramework(path))
        {
            return null;
        }

        if (!_shadows.TryGetValue(path, out Compilation? shadow))
        {
            _shadows[path] = shadow = BuildShadow(assembly.Name, path);
        }

        string id = DocumentationCommentId.CreateDeclarationId(called.OriginalDefinition)!;
        return DocumentationCommentId.GetFirstSymbolForDeclarationId(id, shadow)  is IMethodSymbol { DeclaringSyntaxReferences.IsDefaultOrEmpty: false } method
            ? new MethodSource(method, shadow)
            : null;
    }

    private Compilation? BuildShadow(string assemblyName, string path)
    {
        var settings = new DecompilerSettings(ICSharpCode.Decompiler.CSharp.LanguageVersion.Latest)
        {
            ThrowOnAssemblyResolveErrors = false,
            UsingDeclarations = false
        };

        var resolver = new UniversalAssemblyResolver(path, false, null);
        foreach (string dir in _user.References.OfType<PortableExecutableReference>()
                     .Select(r => Path.GetDirectoryName(r.FilePath))
                     .Where(d => d is not null)
                     .Distinct())
        {
            resolver.AddSearchDirectory(dir!);
        }

        string source = new CSharpDecompiler(path, resolver, settings).DecompileWholeModuleAsString();        
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: $"decompiled/{assemblyName}.cs");

        return CSharpCompilation.Create(assemblyName, [tree], _user.References.Where(r => !SamePath(r, path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }
    
    private static bool SamePath(MetadataReference reference, string path) =>
        reference is PortableExecutableReference { FilePath: { } other } &&
        string.Equals(
            Path.GetFullPath(other),
            Path.GetFullPath(path),
            StringComparison.OrdinalIgnoreCase);
}