using System;
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using YotsubaShader.Compiler.Metadata;

namespace YotsubaShader.Rules;

/// <summary>
/// Lee <c>[ShaderTargets]</c> y <c>[ShaderEngines]</c>. Se buscan por nombre porque el analizador no referencia el
/// ensamblado de metadatos. Orden: la clase del shader (o la que la contiene), el ensamblado y, si no hay nada,
/// <see cref="Target.Vulkan_13"/>.
/// </summary>
internal static class TargetResolver
{
    public const string TargetsAttributeName = "YotsubaShader.Compiler.Metadata.ShaderTargetsAttribute";
    public const string EnginesAttributeName = "YotsubaShader.Compiler.Metadata.ShaderEnginesAttribute";

    private static readonly ConcurrentDictionary<(ulong, ulong, bool), TargetSet> Cache = new();

    public static TargetSet For(INamedTypeSymbol? type)
    {
        (AttributeData? targetsAttribute, AttributeData? enginesAttribute) = (Find(type, TargetsAttributeName), Find(type, EnginesAttributeName));

        Target declared = Read(targetsAttribute, out bool hasTargets);
        GameEngine engines = (GameEngine)ReadBits(enginesAttribute);

        // Un [ShaderTargets] vacío (Target.None) no declara nada: se valida como si no hubiera atributo.
        bool isDefault = !hasTargets || declared == Target.None;
        if (isDefault) declared = Target.Vulkan_13;

        return Cache.GetOrAdd(((ulong)declared, (ulong)engines, isDefault), key => new TargetSet((Target)key.Item1, (GameEngine)key.Item2, key.Item3));
    }

    /// <summary>El atributo de la clase (o de una clase que la contiene) o, si no, el del ensamblado.</summary>
    public static AttributeData? Find(INamedTypeSymbol? type, string attributeName)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (FindIn(current.GetAttributes(), attributeName) is { } found) return found;
        }

        return type?.ContainingAssembly is { } assembly ? FindIn(assembly.GetAttributes(), attributeName) : null;
    }

    public static Target Read(AttributeData? attribute, out bool declared)
    {
        declared = attribute is not null;
        return (Target)ReadBits(attribute);
    }

    private static ulong ReadBits(AttributeData? attribute)
    {
        if (attribute is null || attribute.ConstructorArguments.Length != 1) return 0;
        object? value = attribute.ConstructorArguments[0].Value;
        return value is null ? 0 : Convert.ToUInt64(value);
    }

    private static AttributeData? FindIn(System.Collections.Immutable.ImmutableArray<AttributeData> attributes, string name)
    {
        foreach (AttributeData attribute in attributes)
        {
            if (ShaderSymbols.FullName(attribute.AttributeClass) == name) return attribute;
        }

        return null;
    }
}
