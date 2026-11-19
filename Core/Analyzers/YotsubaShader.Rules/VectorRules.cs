using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>Reglas de los tipos marcados con [Vector] (YS0001–YS0010).</summary>
internal static class VectorRules
{
    public static void AnalyzeType(INamedTypeSymbol type, Action<Diagnostic> report)
    {
        bool isVector = ShaderSymbols.HasAttribute(type, ShaderNames.Vector);

        var components = new List<(ISymbol Member, ITypeSymbol Type, int Index, Location Location)>();
        foreach (ISymbol member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared) continue;
            AttributeData? attribute = member.GetAttributes()
                .FirstOrDefault(a => ShaderSymbols.FullName(a.AttributeClass) == ShaderNames.VectorComponent);
            ITypeSymbol? memberType = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null
            };

            if (attribute is null)
            {
                if (isVector && memberType is not null && !member.IsStatic && member is IFieldSymbol { IsConst: false } or IPropertySymbol { IsIndexer: false })
                {
                    report(Diagnostic.Create(ShaderRules.VectorMemberWithoutComponent, DeclarationRules.LocationOf(member), member.Name, type.Name));
                }

                continue;
            }

            if (!isVector)
            {
                report(Diagnostic.Create(ShaderRules.VectorComponentOutsideVector, DeclarationRules.LocationOf(member), member.Name, type.Name));
                continue;
            }

            if (member.IsStatic || member is IPropertySymbol { IsIndexer: true } || memberType is null)
            {
                report(Diagnostic.Create(ShaderRules.VectorComponentIgnored, DeclarationRules.LocationOf(member), member.Name));
                continue;
            }

            int index = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int i ? i : 0;
            components.Add((member, memberType, index, DeclarationRules.LocationOf(member)));
        }

        if (!isVector) return;

        if (components.Count is < 2 or > 4)
        {
            report(Diagnostic.Create(ShaderRules.VectorComponentCount, DeclarationRules.LocationOf(type), type.Name, components.Count));
        }

        var indices = new HashSet<int>();
        foreach (var component in components)
        {
            if (component.Index < 0)
            {
                report(Diagnostic.Create(ShaderRules.VectorNegativeIndex, component.Location, component.Member.Name, component.Index));
            }
            else if (!indices.Add(component.Index))
            {
                report(Diagnostic.Create(ShaderRules.VectorDuplicateIndex, component.Location, component.Index, type.Name));
            }

            if (component.Type is not ITypeParameterSymbol && GpuType.ScalarOf(component.Type) == GpuScalar.None)
            {
                report(Diagnostic.Create(ShaderRules.VectorComponentType, component.Location, component.Member.Name,
                    component.Type.ToDisplayString()));
            }
        }

        for (int i = 0; i < components.Count; i++)
        {
            if (!indices.Contains(i) && indices.Count > 0 && indices.All(x => x >= 0))
            {
                report(Diagnostic.Create(ShaderRules.VectorMissingIndex, DeclarationRules.LocationOf(type), type.Name, i));
                break;
            }
        }

        var first = components.FirstOrDefault();
        foreach (var component in components.Skip(1))
        {
            if (!SymbolEqualityComparer.Default.Equals(component.Type, first.Type))
            {
                report(Diagnostic.Create(ShaderRules.VectorMixedTypes, component.Location, type.Name,
                    first.Type.ToDisplayString(), component.Type.ToDisplayString()));
                break;
            }
        }
    }
}
