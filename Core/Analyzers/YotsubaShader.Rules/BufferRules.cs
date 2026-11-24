using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>Qué puede ir dentro de un uniform/storage buffer (layout std140/std430).</summary>
internal static class BufferRules
{
    public static void Analyze(ISymbol resource, INamedTypeSymbol structure, ResourceKind kind, ShaderTargets targets, Action<Diagnostic> report)
    {
        AnalyzeStruct(resource.Name, structure, kind, targets, report, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
    }

    /// <summary>
    /// ConstantBuffer&lt;T&gt;: T es el bloque (debe ser un struct). (RW)StructuredBuffer&lt;T&gt;: T es el elemento de
    /// un array sin tamaño (escalar, vector, matriz o struct).
    /// </summary>
    public static void AnalyzeWrapper(ISymbol resource, ITypeSymbol element, ResourceKind kind, ShaderTargets targets, Action<Diagnostic> report)
    {
        GpuType gpuType = GpuType.Of(element);
        Location location = DeclarationRules.LocationOf(resource);
        string typeName = element.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        if (kind == ResourceKind.UniformBuffer)
        {
            if (gpuType.Kind != GpuTypeKind.Struct)
            {
                report(Diagnostic.Create(ShaderRules.BufferNotStruct, location, resource.Name, typeName));
                return;
            }

            AnalyzeStruct(resource.Name, (INamedTypeSymbol)element, kind, targets, report, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
            return;
        }

        string? problem = gpuType.Kind switch
        {
            GpuTypeKind.Unsupported or GpuTypeKind.Array => Loc.T("cannot be the element of a StructuredBuffer"),
            GpuTypeKind.Texture or GpuTypeKind.Sampler => Loc.T("textures and samplers cannot go inside a buffer"),
            _ when gpuType.Scalar == GpuScalar.Bool => Loc.T("bool is not allowed in a buffer shared with the CPU; use uint"),
            _ => null
        };

        if (problem is not null)
        {
            report(Diagnostic.Create(ShaderRules.BufferMemberType, location, "T", resource.Name, typeName, problem));
        }
        else if (gpuType.Kind == GpuTypeKind.Struct)
        {
            AnalyzeStruct(resource.Name, (INamedTypeSymbol)element, kind, targets, report, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
        }
    }

    private static void AnalyzeStruct(string bufferName, INamedTypeSymbol structure, ResourceKind kind, ShaderTargets targets,
        Action<Diagnostic> report, HashSet<ITypeSymbol> visiting)
    {
        if (!visiting.Add(structure)) return;

        var members = structure.GetMembers()
            .Where(m => !m.IsStatic && !m.IsImplicitlyDeclared)
            .Select(m => (Member: m, Type: m switch
            {
                IFieldSymbol { IsConst: false } field => field.Type,
                IPropertySymbol { IsIndexer: false } property => property.Type,
                _ => null
            }))
            .Where(m => m.Type is not null)
            .ToList();

        for (int i = 0; i < members.Count; i++)
        {
            ISymbol member = members[i].Member;
            ITypeSymbol type = members[i].Type!;
            GpuType gpuType = GpuType.Of(type);
            Location location = DeclarationRules.LocationOf(member);
            string typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            string? problem = null;
            switch (gpuType.Kind)
            {
                case GpuTypeKind.Unsupported:
                    problem = Loc.T("has no GPU representation");
                    break;
                case GpuTypeKind.Texture:
                case GpuTypeKind.Sampler:
                    problem = Loc.T("textures and samplers cannot go inside a buffer; declare them as fields of the shader");
                    break;
                case GpuTypeKind.Scalar when gpuType.Scalar == GpuScalar.Bool:
                case GpuTypeKind.Vector when gpuType.Scalar == GpuScalar.Bool:
                    problem = Loc.T("bool is not allowed in a buffer shared with the CPU; use uint");
                    break;
                case GpuTypeKind.Matrix when !SupportedFeatures.MatricesInBuffers:
                    problem = Loc.T("matrices inside buffers are not supported by the compiler yet");
                    break;
                case GpuTypeKind.Array:
                    problem = ArrayProblem(member, gpuType, kind, i == members.Count - 1, structure, report);
                    break;
                case GpuTypeKind.Struct:
                    AnalyzeStruct(bufferName, (INamedTypeSymbol)type, kind, targets, report, visiting);
                    break;
            }

            if (problem is not null)
            {
                report(Diagnostic.Create(ShaderRules.BufferMemberType, location, member.Name, bufferName, typeName, problem));
                continue;
            }

            if (gpuType.Scalar is GpuScalar.Half or GpuScalar.Int16 or GpuScalar.UInt16)
            {
                ShaderTargets affected = targets & (ShaderTargets.Vulkan | ShaderTargets.WebGPU | ShaderTargets.D3D12 | ShaderTargets.OpenGL);
                if (affected != ShaderTargets.None)
                {
                    report(Diagnostic.Create(ShaderRules.PortableLayout, location, member.Name, bufferName,
                        Loc.T("is 16-bit: in a buffer it needs storageBuffer16BitAccess/uniformAndStorageBuffer16BitAccess in Vulkan, -enable-16bit-types in Direct3D 12 and 'shader-f16' in WebGPU (which has no 16-bit integers)"),
                        ShaderTargetsParser.Describe(affected)));
                }
            }
        }

        visiting.Remove(structure);
    }

    private static string? ArrayProblem(ISymbol member, GpuType array, ResourceKind kind, bool isLast, INamedTypeSymbol structure,
        Action<Diagnostic> report)
    {
        GpuType element = GpuType.Of(array.Element);
        if (element.Kind is GpuTypeKind.Unsupported or GpuTypeKind.Array or GpuTypeKind.Texture or GpuTypeKind.Sampler)
        {
            return Loc.T("the elements of an array in a buffer must be scalars, vectors, matrices or structs");
        }

        if (element.Kind == GpuTypeKind.Scalar && element.Scalar == GpuScalar.Bool)
        {
            return Loc.T("bool is not allowed in a buffer shared with the CPU; use uint");
        }

        bool hasLength = ShaderSymbols.HasAttribute(member, ShaderNames.ArrayLength);
        if (hasLength) return SupportedFeatures.FixedArraysInBuffers ? null : Loc.T("arrays inside buffers are not supported by the compiler yet");

        if (kind == ResourceKind.UniformBuffer)
        {
            return Loc.T("a uniform buffer needs the array length: add [ArrayLength(n)]");
        }

        if (!SupportedFeatures.RuntimeArrays)
        {
            return Loc.T("unsized arrays are not supported by the compiler yet");
        }

        if (!isLast)
        {
            report(Diagnostic.Create(ShaderRules.RuntimeArrayNotLast, DeclarationRules.LocationOf(member), member.Name, structure.Name));
        }

        return null;
    }
}

/// <summary>
/// Lo que el compilador ya sabe traducir. Cuando una de estas funciones se implementa, su regla deja de dar
/// error: así el IDE nunca acepta algo que el compilador rechaza.
/// </summary>
internal static class SupportedFeatures
{
    public static readonly bool MatricesInBuffers = true;
    public static readonly bool FixedArraysInBuffers = true;
    public static readonly bool RuntimeArrays = true;
    public static readonly bool LocalFunctions = true;
    public static readonly bool Switch = true;
    public static readonly bool DefaultValue = true;
    public static readonly bool Deconstruction = true;
    public static readonly bool ForEachOverVectors = false;
    public static readonly bool ArraysOfAnyLength = false;
    public static readonly bool MathPolyfills = true;
    public static readonly bool RefParameters = true;
    public static readonly bool Int64 = false;
}
