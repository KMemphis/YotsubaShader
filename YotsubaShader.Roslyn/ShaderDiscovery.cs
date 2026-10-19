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

        ImmutableArray<ParameterInfo> parameters = new ImmutableArray<ParameterInfo>();
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

            parameters = method.Parameters
                .Select(p => new ParameterInfo(p, false))
                .ToImmutableArray();
            
            var visitedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            
            GetVisitedMethods(visitedMethods, method);
            entryPoints.Add(new EntryPointInfo(method, stage, parameters, method.ReturnType, visitedMethods));
        }

        
        
        return entryPoints.ToImmutable();
    }
    
    private static void GetVisitedMethods(HashSet<IMethodSymbol> visitedMethods, IMethodSymbol method)
    {
        throw new NotImplementedException();
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
