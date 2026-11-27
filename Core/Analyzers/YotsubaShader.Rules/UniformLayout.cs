using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>Tamaño de un uniform buffer con el layout std140 (el de Vulkan, OpenGL, WebGPU y el empaquetado de cbuffer de HLSL).</summary>
internal static class UniformLayout
{
    /// <summary>YS0713: el bloque no cabe en lo que alguna API declarada garantiza.</summary>
    public static void CheckSize(ISymbol resource, INamedTypeSymbol block, TargetSet targets, Action<Diagnostic> report)
    {
        long size = SizeOfStruct(block, 0, out _);
        var exceeding = targets.Exceeding((int)Math.Min(size, int.MaxValue), profile => profile.MaxUniformBufferBytes);
        if (exceeding.Count == 0) return;

        DiagnosticDescriptor descriptor = targets.IsDefault ? ShaderRules.UniformBufferTooLarge : ShaderRules.AsError(ShaderRules.UniformBufferTooLarge);
        string limits = string.Join(", ", exceeding.Select(p => Loc.F("{0} ({1} bytes)", p.Name, p.MaxUniformBufferBytes!.Value)));
        report(Diagnostic.Create(descriptor, DeclarationRules.LocationOf(resource), resource.Name, size, limits, TargetSet.Describe(exceeding)));
    }

    private static long Align(long value, long alignment) => (value + alignment - 1) / alignment * alignment;

    private static long SizeOfStruct(INamedTypeSymbol type, int depth, out long alignment)
    {
        long offset = 0;
        alignment = 16;
        if (depth > 8) return 0;

        foreach (ISymbol member in type.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared) continue;
            ITypeSymbol? memberType = member switch
            {
                IFieldSymbol { IsConst: false } field => field.Type,
                IPropertySymbol { IsIndexer: false } property => property.Type,
                _ => null
            };
            if (memberType is null) continue;

            long size = SizeOf(member, memberType, depth, out long memberAlignment);
            offset = Align(offset, memberAlignment) + size;
        }

        return Align(offset, 16);
    }

    private static long SizeOf(ISymbol member, ITypeSymbol type, int depth, out long alignment)
    {
        GpuType gpu = GpuType.Of(type);
        long word = gpu.Scalar is GpuScalar.Double or GpuScalar.Int64 or GpuScalar.UInt64 ? 8 : gpu.Scalar is GpuScalar.Half or GpuScalar.Int16 or GpuScalar.UInt16 ? 2 : 4;
        switch (gpu.Kind)
        {
            case GpuTypeKind.Scalar:
                alignment = word;
                return word;
            case GpuTypeKind.Vector:
                alignment = Align(word * (gpu.Components == 3 ? 4 : gpu.Components), 1);
                return word * gpu.Components;
            case GpuTypeKind.Matrix:
            {
                // Cada columna es un vec4 en un array de stride 16 (32 con doble precisión).
                long column = Align(word * gpu.Components, 16);
                alignment = 16;
                return column * gpu.Columns;
            }
            case GpuTypeKind.Struct:
            {
                long structSize = SizeOfStruct((INamedTypeSymbol)type, depth + 1, out _);
                alignment = 16;
                return structSize;
            }
            case GpuTypeKind.Array:
            {
                long length = ArrayLength(member);
                long elementSize = SizeOf(member, gpu.Element!, depth + 1, out _);
                alignment = 16;
                return Align(elementSize, 16) * length;
            }
            default:
                alignment = 4;
                return 0;
        }
    }

    private static long ArrayLength(ISymbol member)
    {
        foreach (AttributeData attribute in member.GetAttributes())
        {
            if (ShaderSymbols.FullName(attribute.AttributeClass) == ShaderNames.ArrayLength &&
                attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is { } value)
            {
                return Convert.ToInt64(value);
            }
        }

        return 0;
    }
}
