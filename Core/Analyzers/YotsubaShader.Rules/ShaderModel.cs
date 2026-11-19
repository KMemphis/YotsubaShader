using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>Etapa de un punto de entrada. Los valores coinciden con <c>YotsubaShader.Language.Stage</c>.</summary>
internal enum ShaderStageKind
{
    Vertex = 0,
    Fragment = 1,
    Compute = 2
}

/// <summary>Sentido de un miembro de la interfaz de un stage.</summary>
internal enum InterfaceDirection
{
    Input,
    Output
}

/// <summary>Nombres de metadatos de los atributos del lenguaje. Las reglas comparan por nombre para no depender de los ensamblados.</summary>
internal static class ShaderNames
{
    public const string ShaderStage = "YotsubaShader.Language.ShaderStageAttribute";
    public const string ShaderIgnore = "YotsubaShader.Language.Compiler.ShaderIgnore";
    public const string CompilerGenerated = "YotsubaShader.Language.Compiler.CompilerGeneratedCodeAttribute";
    public const string Vector = "YotsubaShader.Language.Native.Types.Vector.VectorAttribute";
    public const string VectorComponent = "YotsubaShader.Language.Native.Types.Vector.VectorComponentAttribute";
    public const string Matrix = "YotsubaShader.Language.Native.Types.Matrix.MatrixAttribute";
    public const string MatrixColumn = "YotsubaShader.Language.Native.Types.Matrix.MatrixColumnAttribute";
    public const string Texture = "YotsubaShader.Language.Native.Types.Texture.TextureAttribute";
    public const string Sampler = "YotsubaShader.Language.Native.Types.SamplerAttribute";
    public const string ArrayLength = "YotsubaShader.Language.ArrayLengthAttribute";
    public const string IntrinsicMethod = "YotsubaShader.Concepts.Methods.IntrinsicMethodAttribute";
    public const string IntrinsicOption = "YotsubaShader.Concepts.Methods.IntrinsicOptionAttribute";
    public const string ConceptKey = "YotsubaShader.Concepts.ConceptKeyAttribute";
    public const string ConceptNamespace = "YotsubaShader.Concepts";
}

/// <summary>Nombres de los conceptos (atributos de <c>YotsubaShader.Concepts</c>) sin el sufijo <c>Attribute</c>.</summary>
internal static class Concepts
{
    public const string Location = "Location";
    public const string UserSemantic = "UserSemantic";
    public const string BindingSlot = "BindingSlot";
    public const string TextureSlot = "TextureSlot";
    public const string SamplerSlot = "SamplerSlot";
    public const string UniformBuffer = "UniformBuffer";
    public const string RWStorageBuffer = "RWStorageBuffer";
    public const string ReadOnlyStorageBuffer = "ReadOnlyStorageBuffer";
    public const string WorkgroupSizeMethod = "WorkgroupSizeMethod";
    public const string ClipSpacePosition = "ClipSpacePosition";
    public const string FragmentCoordinate = "FragmentCoordinate";
}

/// <summary>Un atributo de YotsubaShader ya interpretado: sus conceptos y sus argumentos por clave.</summary>
internal sealed class ConceptAttribute
{
    public ConceptAttribute(AttributeData data, IReadOnlyCollection<string> concepts, IReadOnlyDictionary<string, object?> keys)
    {
        Data = data;
        Concepts = concepts;
        Keys = keys;
    }

    public AttributeData Data { get; }

    /// <summary>Nombre corto del atributo tal y como lo escribe el usuario (SV_Position, location...).</summary>
    public string Name => Data.AttributeClass?.Name ?? "?";

    public IReadOnlyCollection<string> Concepts { get; }

    /// <summary>Argumentos del constructor marcados con [ConceptKey] (Index, Set, Binding, X...).</summary>
    public IReadOnlyDictionary<string, object?> Keys { get; }

    public bool Is(string concept) => Concepts.Contains(concept);

    public int? IntKey(string key) => Keys.TryGetValue(key, out object? value) ? AsInt(value) : null;

    public Location? GetLocation() =>
        Data.ApplicationSyntaxReference is { } reference
            ? Microsoft.CodeAnalysis.Location.Create(reference.SyntaxTree, reference.Span)
            : null;

    private static int? AsInt(object? value) => value switch
    {
        int i => i,
        uint u => (int)u,
        short s => s,
        ushort us => us,
        long l => (int)l,
        byte b => b,
        _ => null
    };
}

internal static class ShaderSymbols
{
    public static bool HasAttribute(ISymbol symbol, string metadataName) =>
        symbol.GetAttributes().Any(a => FullName(a.AttributeClass) == metadataName);

    public static string? FullName(ITypeSymbol? type) =>
        type is null ? null : type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString() + "." + MetadataName(type)
            : MetadataName(type);

    private static string MetadataName(ITypeSymbol type)
    {
        string name = type.MetadataName;
        for (INamedTypeSymbol? outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            name = outer.MetadataName + "+" + name;
        }

        return name;
    }

    /// <summary>La etapa de un método, si tiene [ShaderStage]. Varios atributos devuelven todos.</summary>
    public static IReadOnlyList<(ShaderStageKind Stage, AttributeData Attribute)> GetStages(IMethodSymbol method)
    {
        var result = new List<(ShaderStageKind, AttributeData)>();
        foreach (AttributeData attribute in method.GetAttributes())
        {
            if (FullName(attribute.AttributeClass) != ShaderNames.ShaderStage) continue;
            if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is int value)
            {
                result.Add(((ShaderStageKind)value, attribute));
            }
        }

        return result;
    }

    public static bool IsEntryPoint(IMethodSymbol method) => GetStages(method).Count > 0;

    /// <summary>Un tipo con al menos un punto de entrada es un shader.</summary>
    public static bool IsShaderType(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IMethodSymbol>().Any(IsEntryPoint);

    public static IEnumerable<IMethodSymbol> EntryPoints(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IMethodSymbol>().Where(IsEntryPoint);

    /// <summary>Los atributos de YotsubaShader de un símbolo con sus conceptos y argumentos.</summary>
    public static IReadOnlyList<ConceptAttribute> GetConceptAttributes(ISymbol symbol) =>
        GetConceptAttributes(symbol.GetAttributes());

    public static IReadOnlyList<ConceptAttribute> GetReturnConceptAttributes(IMethodSymbol method) =>
        GetConceptAttributes(method.GetReturnTypeAttributes());

    public static IReadOnlyList<ConceptAttribute> GetConceptAttributes(IEnumerable<AttributeData> attributes)
    {
        var result = new List<ConceptAttribute>();
        foreach (AttributeData attribute in attributes)
        {
            INamedTypeSymbol? type = attribute.AttributeClass;
            if (type is null) continue;

            var concepts = new HashSet<string>();
            if (IsConceptType(type)) concepts.Add(ShortConceptName(type));

            foreach (AttributeData meta in type.GetAttributes())
            {
                if (meta.AttributeClass is { } metaType && IsConceptType(metaType))
                {
                    concepts.Add(ShortConceptName(metaType));
                }
            }

            if (concepts.Count == 0) continue;
            result.Add(new ConceptAttribute(attribute, concepts, ReadKeys(attribute)));
        }

        return result;
    }

    private static bool IsConceptType(INamedTypeSymbol type)
    {
        string? ns = type.ContainingNamespace?.ToDisplayString();
        return ns is not null &&
               (ns == ShaderNames.ConceptNamespace || ns.StartsWith(ShaderNames.ConceptNamespace + ".")) &&
               type.Name != "YotsubaShaderConcept" && type.Name != "ConceptKeyAttribute" &&
               !ns.EndsWith(".Methods");
    }

    private static string ShortConceptName(INamedTypeSymbol type) =>
        type.Name.EndsWith("Attribute") ? type.Name.Substring(0, type.Name.Length - "Attribute".Length) : type.Name;

    private static IReadOnlyDictionary<string, object?> ReadKeys(AttributeData attribute)
    {
        var keys = new Dictionary<string, object?>();
        IMethodSymbol? constructor = attribute.AttributeConstructor;
        if (constructor is null) return keys;

        for (int i = 0; i < constructor.Parameters.Length && i < attribute.ConstructorArguments.Length; i++)
        {
            foreach (AttributeData parameterAttribute in constructor.Parameters[i].GetAttributes())
            {
                if (FullName(parameterAttribute.AttributeClass) != ShaderNames.ConceptKey) continue;
                if (parameterAttribute.ConstructorArguments.Length == 1 &&
                    parameterAttribute.ConstructorArguments[0].Value is string key)
                {
                    TypedConstant argument = attribute.ConstructorArguments[i];
                    keys[key] = argument.Kind == TypedConstantKind.Array ? null : argument.Value;
                }
            }
        }

        return keys;
    }
}
