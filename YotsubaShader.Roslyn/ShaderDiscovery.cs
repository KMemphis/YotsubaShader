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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using YotsubaShader.Language;
using YotsubaShader.Roslyn.Model;

namespace YotsubaShader.Roslyn;

/// <summary>
/// Recorre los tipos del proyecto y extrae sus shaders. Trabaja con símbolos, no con texto:
/// da igual cómo esté escrito el atributo (alias, <c>using</c>, nombre largo o corto).
/// Quien llama debe comprobar antes que la compilación no tenga errores de C#.
/// </summary>
public static class ShaderDiscovery
{
    public const string ShaderStageAttributeMetadataName = "YotsubaShader.Language.ShaderStageAttribute";

    public static ImmutableArray<ShaderInfo> Find(Compilation compilation, CancellationToken cancellationToken = default)
    {
        INamedTypeSymbol? stageAttribute = compilation.GetTypeByMetadataName(ShaderStageAttributeMetadataName);
        if (stageAttribute is null)
        {
            return [];
        }
        
        var shaders = ImmutableArray.CreateBuilder<ShaderInfo>();

        // Assembly.GlobalNamespace: solo los tipos de ESTE proyecto, no los de las referencias.
        foreach (INamedTypeSymbol type in AllTypes(compilation.Assembly.GlobalNamespace))
        {
            cancellationToken.ThrowIfCancellationRequested();

            ImmutableArray<EntryPointInfo> entryPoints = FindEntryPoints(type, stageAttribute);
            if (!entryPoints.IsEmpty)
            {
                shaders.Add(new ShaderInfo(type, entryPoints));
            }
        }

        return shaders.ToImmutable();
    }

    private static ImmutableArray<EntryPointInfo> FindEntryPoints(INamedTypeSymbol type, INamedTypeSymbol stageAttribute)
    {
        var entryPoints = ImmutableArray.CreateBuilder<EntryPointInfo>();

        foreach (ISymbol member in type.GetMembers())
        {
            // Solo métodos normales: GetMembers también devuelve constructores y accesores de propiedades.
            if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary } method)
            {
                continue;
            }

            if (!TryGetStage(method, stageAttribute, out Stage stage))
            {
                continue;
            }

            ImmutableArray<ParameterInfo> parameters = method.Parameters
                .Select(p => new ParameterInfo(p))
                .ToImmutableArray();

            entryPoints.Add(new EntryPointInfo(method, stage, parameters, method.ReturnType));
        }

        return entryPoints.ToImmutable();
    }

    private static bool TryGetStage(IMethodSymbol method, INamedTypeSymbol stageAttribute, out Stage stage)
    {
        // GetAttributes() solo devuelve los atributos aplicados directamente al método
        // (no los de un método que este sobrescribe).
        foreach (AttributeData attribute in method.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, stageAttribute))
            {
                continue;
            }

            // El valor de un enum llega como su número, no como su nombre.
            if (attribute.ConstructorArguments is [{ Value: int value }] && Enum.IsDefined(typeof(Stage), value))
            {
                stage = (Stage)value;
                return true;
            }
        }

        stage = default;
        return false;
    }

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol ns)
    {
        foreach (INamedTypeSymbol type in ns.GetTypeMembers())
        {
            foreach (INamedTypeSymbol t in WithNested(type))
            {
                yield return t;
            }
        }

        foreach (INamespaceSymbol child in ns.GetNamespaceMembers())
        {
            foreach (INamedTypeSymbol t in AllTypes(child))
            {
                yield return t;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> WithNested(INamedTypeSymbol type)
    {
        yield return type;

        foreach (INamedTypeSymbol nested in type.GetTypeMembers())
        {
            foreach (INamedTypeSymbol t in WithNested(nested))
            {
                yield return t;
            }
        }
    }
}
